using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Validators.Repository;
public class RepositoryConflictRequestValidator : AbstractValidator<RepositoryConflictRequest>
{
    public RepositoryConflictRequestValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("conflict.invalid"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("conflict.invalid"));
        RuleFor(x => x.Path).Must(value => !string.IsNullOrEmpty(value) && value.Length <= 2000 && !value.Contains('\0')).WithMessage(_ => LocalizedText.Get("conflict.invalid"));
    }
}
public class ResolveConflictRequestValidator : AbstractValidator<ResolveConflictRequest>
{
    public ResolveConflictRequestValidator()
    {
        RuleFor(x => x.File).NotNull().WithMessage(_ => LocalizedText.Get("conflict.invalid"));
        RuleFor(x => x.File).SetValidator(new RepositoryConflictRequestValidator());
        RuleFor(x => x.Action).Must(value => value is "edit" or "ours" or "theirs" or "delete" or "stage").WithMessage(_ => LocalizedText.Get("conflict.invalid"));
        RuleFor(x => x.Content).Must(value => value is not null && value.Length <= 200_000 && !value.Contains('\0')).WithMessage(_ => LocalizedText.Get("conflict.invalid"));
        RuleFor(x => x.Token).Must(value => value is not null && value.Length == 64 && value.All(Uri.IsHexDigit)).WithMessage(_ => LocalizedText.Get("conflict.invalid"));
    }
}
