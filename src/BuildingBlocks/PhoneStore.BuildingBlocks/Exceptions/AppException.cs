using System.Net;

namespace PhoneStore.BuildingBlocks.Exceptions;

public class AppException : Exception
{
    public string Code { get; }
    public HttpStatusCode StatusCode { get; }

    public AppException(string message, string code = "INTERNAL_SERVER_ERROR", HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message, string code = "NOT_FOUND")
        : base(message, code, HttpStatusCode.NotFound)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message, string code = "CONFLICT")
        : base(message, code, HttpStatusCode.Conflict)
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Chưa được xác thực hoặc phiên đăng nhập đã hết hạn.", string code = "UNAUTHORIZED")
        : base(message, code, HttpStatusCode.Unauthorized)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Không có quyền thực hiện thao tác này.", string code = "FORBIDDEN")
        : base(message, code, HttpStatusCode.Forbidden)
    {
    }
}

public class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors, string message = "Dữ liệu yêu cầu không hợp lệ.")
        : base(message, "VALIDATION_ERROR", HttpStatusCode.BadRequest)
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = [error] })
    {
    }
}
