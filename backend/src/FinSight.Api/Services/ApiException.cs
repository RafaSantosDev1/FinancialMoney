namespace FinSight.Services;

public class ApiException(int statusCode, string message, IReadOnlyList<string>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyList<string>? Errors { get; } = errors;

    public static ApiException BadRequest(string message, IReadOnlyList<string>? errors = null) => new(400, message, errors);
    public static ApiException NotFound(string message) => new(404, message);
    public static ApiException Conflict(string message) => new(409, message);
}
