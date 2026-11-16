namespace PhoneStore.BuildingBlocks.Common;

/// <summary>
/// N2LHTBLTT-30: Cấu hình kết nối MinIO (S3-compatible object storage).
/// Bind từ section "MinioStorage" trong appsettings.json.
/// </summary>
public class MinioStorageOptions
{
    public const string SectionName = "MinioStorage";

    /// <summary>
    /// Endpoint của MinIO server (ví dụ: "http://localhost:9000").
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Access Key (username) của MinIO.
    /// </summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret Key (password) của MinIO.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Tên bucket trên MinIO (ví dụ: "phonestore-images").
    /// </summary>
    public string BucketName { get; set; } = "phonestore-images";

    /// <summary>
    /// URL public để truy cập ảnh từ bên ngoài.
    /// Nếu để trống, sẽ dùng Endpoint + BucketName.
    /// Ví dụ: "http://localhost:9000" → URL ảnh = "http://localhost:9000/phonestore-images/products/1/abc.jpg"
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
