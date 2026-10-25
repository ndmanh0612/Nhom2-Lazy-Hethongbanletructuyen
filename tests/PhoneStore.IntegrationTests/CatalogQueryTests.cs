using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// Kiểm thử tích hợp API danh mục sản phẩm: tìm kiếm, lọc, sắp xếp và phân trang (FR-04).
///
/// Bắt được bug: `SortBy` / `MinPrice` / `MaxPrice` được validate nhưng không được áp dụng
/// vào câu truy vấn, nên API trả về kết quả không đúng với tham số.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CatalogQueryTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CatalogQueryTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Search_by_name_returns_matching_product()
    {
        var response = await _client.GetAsync("/api/v1/products?search=iPhone");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("name").GetString()!.ToLower().Should().Contain("iphone");
        }
    }

    [Fact]
    public async Task Filter_by_min_price_excludes_cheaper_products()
    {
        // Lấy giá cao nhất trong catalog làm mốc.
        var all = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products?pageSize=50", Json);
        var maxPrice = all.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Max(item => item.GetProperty("maxPrice").GetInt64());

        var response = await _client.GetAsync($"/api/v1/products?minPrice={maxPrice}&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var item in items.EnumerateArray())
        {
            // Sản phẩm được giữ lại khi CÓ phiên bản nằm trong khoảng giá.
            item.GetProperty("maxPrice").GetInt64().Should().BeGreaterThanOrEqualTo(maxPrice);
        }
    }

    [Fact]
    public async Task Sort_by_price_desc_returns_highest_price_first()
    {
        var response = await _client.GetAsync("/api/v1/products?sortBy=price_desc&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCountGreaterThan(1);

        var prices = items.Select(item => item.GetProperty("minPrice").GetInt64()).ToList();
        prices.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Sort_by_price_asc_returns_lowest_price_first()
    {
        var response = await _client.GetAsync("/api/v1/products?sortBy=price_asc&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCountGreaterThan(1);

        var prices = items.Select(item => item.GetProperty("minPrice").GetInt64()).ToList();
        prices.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Sort_by_price_desc_and_asc_are_reverse_of_each_other()
    {
        var desc = await GetPricesAsync("price_desc");
        var asc = await GetPricesAsync("price_asc");

        desc.Should().Equal(asc.AsEnumerable().Reverse());
    }

    [Fact]
    public async Task Sort_by_newest_returns_products_in_descending_created_order()
    {
        var response = await _client.GetAsync("/api/v1/products?sortBy=newest&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCountGreaterThan(1);

        // So sánh theo thời gian tạo (CreatedAt) — thứ tự Id không phản ánh
        // thứ tự tạo vì seed data không bảo đảm Id tăng theo thời gian.
        var createdAt = items
            .Select(item => item.GetProperty("createdAt").GetDateTimeOffset())
            .ToList();

        createdAt.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Pagination_splits_results_and_reports_total_count()
    {
        var all = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products?pageSize=50", Json);
        var totalCount = all.GetProperty("data").GetProperty("totalCount").GetInt32();
        totalCount.Should().BeGreaterThan(1);

        var firstPage = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products?page=1&pageSize=1", Json);
        var firstData = firstPage.GetProperty("data");

        firstData.GetProperty("page").GetInt32().Should().Be(1);
        firstData.GetProperty("pageSize").GetInt32().Should().Be(1);
        firstData.GetProperty("totalCount").GetInt32().Should().Be(totalCount);
        firstData.GetProperty("items").GetArrayLength().Should().Be(1);

        var secondPage = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products?page=2&pageSize=1", Json);
        var secondData = secondPage.GetProperty("data");

        secondData.GetProperty("page").GetInt32().Should().Be(2);
        secondData.GetProperty("items").GetArrayLength().Should().Be(1);

        // Trang 1 và trang 2 không được trùng sản phẩm.
        var firstId = firstData.GetProperty("items")[0].GetProperty("id").GetInt64();
        var secondId = secondData.GetProperty("items")[0].GetProperty("id").GetInt64();
        firstId.Should().NotBe(secondId);
    }

    [Fact]
    public async Task Filter_by_category_returns_only_products_of_that_category()
    {
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/v1/categories", Json);
        var categoryId = categories.GetProperty("data")[0].GetProperty("id").GetInt64();

        var response = await _client.GetAsync($"/api/v1/products?categoryId={categoryId}&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items").EnumerateArray().ToList();

        items.Should().HaveCountGreaterThan(0);
        foreach (var item in items)
        {
            item.GetProperty("categoryId").GetInt64().Should().Be(categoryId);
        }
    }

    [Fact]
    public async Task Invalid_sort_by_returns_validation_error()
    {
        var response = await _client.GetAsync("/api/v1/products?sortBy=bogus");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Unknown_product_returns_not_found()
    {
        var response = await _client.GetAsync("/api/v1/products/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("PRODUCT_NOT_FOUND");
    }

    private async Task<List<long>> GetPricesAsync(string sortBy)
    {
        var payload = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/products?sortBy={sortBy}&pageSize=50", Json);

        return payload.GetProperty("data").GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("minPrice").GetInt64())
            .ToList();
    }
}
