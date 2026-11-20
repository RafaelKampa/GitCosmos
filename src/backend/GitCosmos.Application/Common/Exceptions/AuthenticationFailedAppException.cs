namespace GitCosmos.Application.Common.Exceptions;

public sealed class AuthenticationFailedAppException : AppException
{
    public AuthenticationFailedAppException(string message)
        : base(message, 401, "authentication_failed")
    {
    }
}
