namespace PhoneStore.BuildingBlocks.Common;

/// <summary>
/// N2LHTBLTT-30: Cấu hình kết nối Cloudflare R2 (S3-compatible storage).
/// Bind từ section "CloudflareR2" trong appsettings.json.
/// </summary>
public class CloudflareR2Options
{
    public const string SectionName = "CloudflareR2";

    /// <summary>
    /// Cloudflare Account ID (tìm trong Cloudflare Dashboard → Overview → API).
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// R2 API Token — Access Key ID.
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// R2 API Token — Secret Access Key.
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Tên bucket trên R2 (ví dụ: "phonestore-images").
    /// </summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>
    /// URL public của R2 bucket (ví dụ: "https://pub-xxx.r2.dev" hoặc custom domain).
    /// Dùng để sinh URL truy cập ảnh công khai.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
