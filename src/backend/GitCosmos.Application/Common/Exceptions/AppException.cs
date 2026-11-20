namespace GitCosmos.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode, string errorCode)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Errors = [message];
    }

    protected AppException(IReadOnlyCollection<string> errors, int statusCode, string errorCode)
        : base(errors.FirstOrDefault() ?? "Application error.")
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Errors = errors.Count == 0 ? ["Application error."] : errors.ToList();
    }

    public int StatusCode { get; }
    public string ErrorCode { get; }
    public IReadOnlyCollection<string> Errors { get; }
}
