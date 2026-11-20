namespace GitCosmos.Application.Common.Exceptions;

public sealed class ExternalServiceAppException : AppException
{
    public ExternalServiceAppException(
        IReadOnlyCollection<string> errors,
        int statusCode,
        string errorCode)
        : base(errors, statusCode, errorCode)
    {
    }
}
