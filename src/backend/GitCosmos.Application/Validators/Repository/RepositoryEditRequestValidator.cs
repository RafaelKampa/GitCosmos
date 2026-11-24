using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Validators.Repository;

public class RepositoryEditRequestValidator : AbstractValidator<RepositoryEditRequest>
{
    public RepositoryEditRequestValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("repository.invalid_id"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.Operation).Must(x => x is "stage" or "unstage" or "commit").WithMessage(_ => LocalizedText.Get("edit.invalid"));
        RuleFor(x => x.Paths).Must(paths => paths is not null && paths.Length <= 2000 && paths.All(path => !string.IsNullOrWhiteSpace(path) && path.Length <= 32767 && !path.Contains('\0')))
            .WithMessage(_ => LocalizedText.Get("edit.invalid"));
        RuleFor(x => x.Subject).Must(value => value is not null && value.Length <= 200 && !value.Contains('\0') && !value.Contains('\n') && !value.Contains('\r'))
            .WithMessage(_ => LocalizedText.Get("edit.invalid"));
        RuleFor(x => x.Description).Must(value => value is not null && value.Length <= 10000 && !value.Contains('\0')).WithMessage(_ => LocalizedText.Get("edit.invalid"));
        RuleFor(x => x).Must(x => x.Operation == "commit" ? !string.IsNullOrWhiteSpace(x.Subject) && x.Paths.Length == 0 : x.Paths.Length > 0 && !x.Amend && !x.Push)
            .When(x => x.Paths is not null).WithMessage(_ => LocalizedText.Get("edit.invalid"));
    }
}
