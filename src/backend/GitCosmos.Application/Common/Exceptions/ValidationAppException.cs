namespace GitCosmos.Application.Common.Exceptions;

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string message)
        : base(message, 400, "validation_error")
    {
    }

    public ValidationAppException(IReadOnlyCollection<string> errors)
        : base(errors, 400, "validation_error")
    {
    }
}
