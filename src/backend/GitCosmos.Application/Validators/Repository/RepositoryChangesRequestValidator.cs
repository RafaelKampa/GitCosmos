using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Validators.Repository;

public class RepositoryChangesRequestValidator : AbstractValidator<RepositoryChangesRequest>
{
    public RepositoryChangesRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("repository.invalid_id"));
    }
}

public class RepositoryDiffRequestValidator : AbstractValidator<RepositoryDiffRequest>
{
    public RepositoryDiffRequestValidator()
    {
        RuleFor(x => x.Path).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => LocalizedText.Get("changes.invalid_selection"))
            .MaximumLength(32767).WithMessage(_ => LocalizedText.Get("changes.invalid_selection"));
        RuleFor(x => x.Area).Must(area => area is "staged" or "working").WithMessage(_ => LocalizedText.Get("changes.invalid_selection"));
    }
}
