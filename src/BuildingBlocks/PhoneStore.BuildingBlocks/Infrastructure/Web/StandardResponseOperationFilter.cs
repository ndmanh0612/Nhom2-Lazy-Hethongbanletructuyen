using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PhoneStore.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Bổ sung mô tả response chuẩn của API vào Swagger.
/// Không ghi đè nội dung mà developer đã khai báo thủ công.
/// </summary>
public class StandardResponseOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var response in operation.Responses)
        {
            if (response.Value.Description is not null)
            {
                continue;
            }

            response.Value.Description = DescribeStatus(response.Key);
        }
    }

    private static string DescribeStatus(string statusCode)
    {
        return statusCode switch
        {
            "200" => "Thành công",
            "201" => "Tạo mới thành công",
            "202" => "Đã tiếp nhận yêu cầu",
            "204" => "Không có nội dung",
            "400" => "Dữ liệu yêu cầu không hợp lệ",
            "401" => "Chưa xác thực",
            "403" => "Không đủ quyền",
            "404" => "Không tìm thấy dữ liệu",
            "409" => "Xung đột trạng thái dữ liệu",
            "500" => "Lỗi hệ thống",
            _ => "Kết quả"
        };
    }
}
