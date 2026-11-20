namespace GitCosmos.Application.Common.Exceptions;

public sealed class AccessDeniedAppException : AppException
{
    public AccessDeniedAppException(string message)
        : base(message, 403, "access_denied")
    {
    }
}
