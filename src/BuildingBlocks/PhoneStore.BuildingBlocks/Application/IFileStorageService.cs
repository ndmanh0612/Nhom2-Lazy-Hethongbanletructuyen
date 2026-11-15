namespace PhoneStore.BuildingBlocks.Application;

/// <summary>
/// N2LHTBLTT-30: Abstraction cho dịch vụ lưu trữ file (S3-compatible).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload file lên storage và trả về object key.
    /// </summary>
    /// <param name="stream">Nội dung file.</param>
    /// <param name="fileName">Tên file gốc (dùng để lấy extension).</param>
    /// <param name="contentType">MIME type (ví dụ: image/jpeg).</param>
    /// <param name="folder">Thư mục con trên bucket (ví dụ: "products/123").</param>
    /// <param name="cancellationToken">Token hủy.</param>
    /// <returns>Object key đã lưu trên storage.</returns>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa file khỏi storage.
    /// </summary>
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trả về URL public (nếu bucket public) hoặc base URL + key.
    /// </summary>
    string GetPublicUrl(string objectKey);

    /// <summary>
    /// N2LHTBLTT-31: Sinh presigned URL cho phép truy cập tạm thời vào một object.
    /// </summary>
    Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan expiration, CancellationToken cancellationToken = default);
}
