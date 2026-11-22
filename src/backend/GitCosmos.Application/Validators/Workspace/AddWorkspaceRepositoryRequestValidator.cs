using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;

namespace GitCosmos.Application.Validators.Workspace;

public class AddWorkspaceRepositoryRequestValidator : AbstractValidator<AddWorkspaceRepositoryRequest>
{
    public AddWorkspaceRepositoryRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.LocalPath).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => LocalizedText.Get("repository.path_required"))
            .MaximumLength(32767).WithMessage(_ => LocalizedText.Get("repository.invalid_path"));
    }
}
