using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Storage;

/// <summary>
/// N2LHTBLTT-30: Triển khai lưu trữ file trên MinIO (S3-compatible).
/// N2LHTBLTT-31: Sinh Presigned URL cho truy cập ảnh.
/// </summary>
public class MinioStorageService : IFileStorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly MinioStorageOptions _options;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IOptions<MinioStorageOptions> options, ILogger<MinioStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credentials = new BasicAWSCredentials(_options.AccessKey, _options.SecretKey);
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true, // MinIO yêu cầu path-style (không dùng virtual-hosted-style).
            AuthenticationRegion = "us-east-1", // MinIO không dùng region nhưng SDK yêu cầu.
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
        // Đảm bảo bucket tồn tại.
        await EnsureBucketExistsAsync(cancellationToken);

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
            // Cache ảnh sản phẩm 1 năm (immutable vì tên file là GUID).
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        };

        await _s3Client.PutObjectAsync(putRequest, cancellationToken);
        _logger.LogInformation("Đã upload file {ObjectKey} lên MinIO bucket {Bucket}.", objectKey, _options.BucketName);

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
        _logger.LogInformation("Đã xóa file {ObjectKey} khỏi MinIO bucket {Bucket}.", objectKey, _options.BucketName);
    }

    /// <inheritdoc />
    public string GetPublicUrl(string objectKey)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? _options.Endpoint.TrimEnd('/')
            : _options.PublicBaseUrl.TrimEnd('/');

        return $"{baseUrl}/{_options.BucketName}/{objectKey}";
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

        var url = await _s3Client.GetPreSignedURLAsync(request);
        return url;
    }

    /// <summary>
    /// Tự động tạo bucket nếu chưa tồn tại và đặt policy public read.
    /// </summary>
    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buckets = await _s3Client.ListBucketsAsync(cancellationToken);
            if (buckets.Buckets.Any(b => b.BucketName == _options.BucketName))
                return;

            await _s3Client.PutBucketAsync(_options.BucketName, cancellationToken);
            _logger.LogInformation("Đã tạo bucket MinIO: {Bucket}.", _options.BucketName);

            // Đặt policy public read cho bucket để ảnh có thể truy cập qua URL.
            var policy = $$"""
            {
                "Version": "2012-10-17",
                "Statement": [
                    {
                        "Effect": "Allow",
                        "Principal": {"AWS": ["*"]},
                        "Action": ["s3:GetObject"],
                        "Resource": ["arn:aws:s3:::{{_options.BucketName}}/*"]
                    }
                ]
            }
            """;

            await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = _options.BucketName,
                Policy = policy,
            }, cancellationToken);
            _logger.LogInformation("Đã đặt policy public read cho bucket {Bucket}.", _options.BucketName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể kiểm tra/tạo bucket MinIO {Bucket}.", _options.BucketName);
        }
    }

    public void Dispose()
    {
        _s3Client?.Dispose();
    }
}
