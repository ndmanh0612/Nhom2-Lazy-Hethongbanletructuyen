using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PhoneStore.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Cấu hình Swagger/OpenAPI dùng chung cho toàn hệ thống:
/// - Đọc XML comments để mô tả endpoint và model.
/// - Gắn mã lỗi chuẩn (VALIDATION_ERROR, NOT_FOUND, ...) vào response.
/// - Bổ sung security scheme Bearer (để dùng sau khi Identity có JWT).
/// </summary>
public static class SwaggerRegistration
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddAppSwagger(
        this IServiceCollection services,
        string title,
        string version = "v1",
        string description = "PhoneStore API — Hệ thống bán lẻ trực tuyến.")
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(version, new OpenApiInfo
            {
                Title = title,
                Version = version,
                Description = description,
                Contact = new OpenApiContact
                {
                    Name = "Nhóm 2 - Lazy",
                    Url = new Uri("https://github.com/ndmanh0612/Nhom2-Lazy-Hethongbanletructuyen")
                }
            });

            // Nạp XML comments của Host (chứa controller) và các module (chứa DTO).
            foreach (var xmlPath in CollectXmlDocumentationPaths())
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            options.OperationFilter<StandardResponseOperationFilter>();

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhập token dạng: Bearer {token}"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = BearerScheme
                        }
                    },
                    Array.Empty<string>()
                }
            });

            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));
        });

        return services;
    }

    private static IEnumerable<string> CollectXmlDocumentationPaths()
    {
        // File XML documentation được copy sẵn vào thư mục output khi build.
        var outputDirectory = AppContext.BaseDirectory;

        foreach (var xmlFile in Directory.EnumerateFiles(outputDirectory, "PhoneStore.*.xml", SearchOption.TopDirectoryOnly))
        {
            yield return xmlFile;
        }
    }
}
