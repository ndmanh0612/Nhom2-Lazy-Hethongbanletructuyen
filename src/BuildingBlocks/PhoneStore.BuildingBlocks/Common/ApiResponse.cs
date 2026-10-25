namespace PhoneStore.BuildingBlocks.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "Thành công";
    public T? Data { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public static ApiResponse<T> Ok(T data, string message = "Thành công") =>
        new() { Success = true, Message = message, Data = data };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Thành công") =>
        new() { Success = true, Message = message, Data = null };
}

public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
