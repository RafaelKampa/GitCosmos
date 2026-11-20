namespace GitCosmos.Application.Common.Exceptions;

public sealed class NotFoundAppException : AppException
{
    public NotFoundAppException(string message)
        : base(message, 404, "not_found")
    {
    }
}
