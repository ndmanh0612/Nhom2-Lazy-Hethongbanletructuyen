using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// N2LHTBLTT-30: Triển khai lưu trữ file trên Cloudflare R2 (S3-compatible).
/// N2LHTBLTT-31: Sinh Presigned URL cho truy cập ảnh.
/// </summary>
public class CloudflareR2StorageService : IFileStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly CloudflareR2Options _options;

    public CloudflareR2StorageService(IOptions<CloudflareR2Options> options)
    {
        _options = options.Value;

        var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_options.AccountId}.r2.cloudflarestorage.com",
            // R2 không dùng region nhưng SDK yêu cầu, đặt us-east-1 theo hướng dẫn Cloudflare.
            AuthenticationRegion = "auto",
        };

        _s3Client = new AmazonS3Client(credentials, config);
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        string folder,
        CancellationToken cancellationToken = default)
    {
        // Sinh tên file duy nhất: products/123/{guid}.jpg
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var objectKey = string.IsNullOrWhiteSpace(folder)
            ? $"{Guid.NewGuid()}{extension}"
            : $"{folder.TrimEnd('/')}/{Guid.NewGuid()}{extension}";

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = contentType,
            // Đặt cache-control cho ảnh sản phẩm (cache 1 năm, immutable vì tên file là GUID).
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        };

        await _s3Client.PutObjectAsync(putRequest, cancellationToken);

        return objectKey;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
        };

        await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
    }

    /// <inheritdoc />
    public string GetPublicUrl(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            // Fallback: trả về S3-style URL nếu chưa cấu hình public domain.
            return $"https://{_options.AccountId}.r2.cloudflarestorage.com/{_options.BucketName}/{objectKey}";
        }

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{objectKey}";
    }

    /// <inheritdoc />
    public async Task<string> GetPresignedUrlAsync(
        string objectKey,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiration),
            Verb = HttpVerb.GET,
        };

        // GetPreSignedURL là synchronous trong SDK v4.
        var url = await _s3Client.GetPreSignedURLAsync(request);
        return url;
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
