using GitCosmos.Application.Common.Exceptions;
using FluentValidation;

namespace GitCosmos.Application.Validators;

public static class ValidationExtensions
{
    public static void ValidateAndThrowApp<T>(this IValidator<T> validator, T instance)
    {
        var result = validator.Validate(instance);
        if (!result.IsValid)
            throw new ValidationAppException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
