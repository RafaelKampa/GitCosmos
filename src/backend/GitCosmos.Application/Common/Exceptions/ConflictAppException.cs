namespace GitCosmos.Application.Common.Exceptions;

public sealed class ConflictAppException : AppException
{
    public ConflictAppException(string message)
        : base(message, 409, "conflict")
    {
    }
}
