using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-39: Helper dùng chung cho bộ regression test.
///
/// Gom các thao tác lặp đi lặp lại (gọi API, đọc dữ liệu từ response, tạo giỏ hàng,
/// checkout) về một chỗ để mỗi test chỉ tập trung vào hành vi cần khẳng định.
/// </summary>
internal static class ApiTestHelper
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// N2LHTBLTT-8 / N2LHTBLTT-9: Tạo client đã xác thực bằng JWT thật.
    ///
    /// Tham số <paramref name="userId"/> được giữ lại để các test cũ không phải
    /// sửa lời gọi, nhưng giá trị này KHÔNG còn dùng để mạo danh — client luôn
    /// được cấp token của một tài khoản khách hàng đã đăng ký thật.
    /// </summary>
    internal static Task<HttpClient> CreateUserClient(PhoneStoreApiFactory factory, long userId)
    {
        return AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
    }

    /// <summary>
    /// N2LHTBLTT-8 / N2LHTBLTT-9: Tạo client đã xác thực bằng JWT thật.
    /// </summary>
    internal static Task<HttpClient> CreateAuthenticatedCustomerAsync(PhoneStoreApiFactory factory)
    {
        return AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
    }

    /// <summary>
    /// Gọi GET và trả về phần `data` của ApiResponse. Yêu cầu phải trả 200.
    /// </summary>
    internal static async Task<JsonElement> GetDataAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "GET {0} phải trả 200 nhưng thực tế trả {1}.", url, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return payload.GetProperty("data");
    }

    /// <summary>
    /// Gọi một request bất kỳ và trả về cả status code lẫn body đã parse.
    /// Nếu response không có body JSON (ví dụ 404 của ASP.NET routing), trả về
    /// JsonValueKind.Undefined để test tự kiểm tra status code.
    /// </summary>
    internal static async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return (response.StatusCode, default);
        }

        try
        {
            return (response.StatusCode, JsonDocument.Parse(raw).RootElement.Clone());
        }
        catch (JsonException)
        {
            return (response.StatusCode, default);
        }
    }

    /// <summary>
    /// Khẳng định body là ErrorResponse chuẩn và trả về mã lỗi.
    /// </summary>
    internal static string AssertErrorEnvelope(JsonElement body, string expectedCode)
    {
        body.TryGetProperty("code", out var code).Should().BeTrue(
            "Response lỗi phải có trường 'code'.");
        code.GetString().Should().Be(expectedCode);

        body.TryGetProperty("message", out var message).Should().BeTrue();
        message.GetString().Should().NotBeNullOrWhiteSpace();

        body.TryGetProperty("requestId", out var requestId).Should().BeTrue(
            "Response lỗi phải có trường 'requestId'.");
        requestId.GetString().Should().NotBeNullOrWhiteSpace();

        body.TryGetProperty("timestamp", out _).Should().BeTrue(
            "Response lỗi phải có trường 'timestamp'.");

        return code.GetString()!;
    }

    /// <summary>
    /// Lấy một phiên bản sản phẩm CỐ ĐỆN để dùng cho các test checkout.
    ///
    /// N2LHTBLTT-22 / N2LHTBLTT-23: danh sách sản phẩm mặc sắp xếp theo "newest", nên
    /// phçn tấu tiên thường là sản phẩm do test quản trị vừa tạo (tồn kho rát thấp,
    /// dễ bệ cạn tốn khi test khác cùng hết). Helper này cố định chọn sản phẩm seed
    /// "iPhone 15" — luôn có đủ tồn kho lớn nên các test checkout không bị cạn tốn.
    /// </summary>
    internal static async Task<(long VariantId, long Price, string Sku)> GetFirstVariantAsync(HttpClient client)
    {
        var products = await GetDataAsync(client, "/api/v1/products?search=iPhone%2015");
        var items = products.GetProperty("items");

        if (items.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Không tìm thấy sản phẩm seed \"iPhone 15\" trong catalog.");
        }

        var productId = items[0].GetProperty("id").GetInt64();

        var detail = await GetDataAsync(client, $"/api/v1/products/{productId}");
        var variant = detail.GetProperty("variants")[0];

        return (
            variant.GetProperty("id").GetInt64(),
            variant.GetProperty("price").GetInt64(),
            variant.GetProperty("sku").GetString()!);
    }

    /// <summary>
    /// Thêm sản phẩm vào giỏ và checkout COD, trả về phần `data` của đơn hàng.
    /// </summary>
    internal static async Task<JsonElement> CheckoutAsync(
        HttpClient client,
        long variantId,
        int quantity = 1)
    {
        var addResponse = await client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity },
            Json);

        addResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "Thêm vào giỏ hàng phải trả 200 nhưng thực tế trả {0}.", addResponse.StatusCode);

        var (status, payload) = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ, Quận 1, TP.HCM"
            });

        status.Should().Be(
            HttpStatusCode.Created,
            "Checkout phải trả 201 nhưng thực tế trả {0}.", status);

        return payload.GetProperty("data");
    }

    /// <summary>
    /// Lấy Id của một danh mục thực sự có sản phẩm.
    ///
    /// Danh sách danh mục được sắp xếp theo tên, nên danh mục đầu tiên có thể là
    /// danh mục rỗng do test khác vừa tạo. Test lọc theo danh mục/brand phải chọn
    /// danh mục có sản phẩm thì mới kiểm tra được.
    /// </summary>
    internal static async Task<long> GetCategoryIdWithProductsAsync(HttpClient client)
    {
        var products = await GetDataAsync(client, "/api/v1/products?pageSize=50");
        var categoryId = products.GetProperty("items").EnumerateArray()
            .First()
            .GetProperty("categoryId").GetInt64();

        var categories = await GetDataAsync(client, "/api/v1/categories");
        categories.EnumerateArray()
            .Should().Contain(category => category.GetProperty("id").GetInt64() == categoryId);

        return categoryId;
    }

    /// <summary>
    /// Lấy Id của một thương hiệu thực sự có sản phẩm.
    /// </summary>
    internal static async Task<long> GetBrandIdWithProductsAsync(HttpClient client)
    {
        var products = await GetDataAsync(client, "/api/v1/products?pageSize=50");
        var brandId = products.GetProperty("items").EnumerateArray()
            .First()
            .GetProperty("brandId").GetInt64();

        var brands = await GetDataAsync(client, "/api/v1/brands");
        brands.EnumerateArray()
            .Should().Contain(brand => brand.GetProperty("id").GetInt64() == brandId);

        return brandId;
    }

    /// <summary>
    /// Thực thi một thao tác với DbContext của module Catalog (dùng để dựng dữ liệu
    /// phục vụ kiểm thử mà chưa có endpoint quản trị tương ứng).
    /// </summary>
    internal static async Task<T> WithCatalogDbAsync<T>(
        PhoneStoreApiFactory factory,
        Func<PhoneStore.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext>();
        return await action(dbContext);
    }
}
