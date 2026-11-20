namespace GitCosmos.Application.Common.Exceptions;

public sealed class OperationFailedAppException : AppException
{
    public OperationFailedAppException(string message)
        : base(message, 500, "operation_failed")
    {
    }
}
