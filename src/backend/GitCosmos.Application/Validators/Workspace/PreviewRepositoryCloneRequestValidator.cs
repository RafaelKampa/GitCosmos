using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;

namespace GitCosmos.Application.Validators.Workspace;

public class PreviewRepositoryCloneRequestValidator : AbstractValidator<PreviewRepositoryCloneRequest>
{
    public PreviewRepositoryCloneRequestValidator()
    {
        RuleFor(x => x.WorkspaceId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.RemoteUrl).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => LocalizedText.Get("clone.invalid_url"))
            .MaximumLength(4096).WithMessage(_ => LocalizedText.Get("clone.invalid_url"));
        RuleFor(x => x.ParentPath).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => LocalizedText.Get("repository.invalid_path"))
            .MaximumLength(32767).WithMessage(_ => LocalizedText.Get("repository.invalid_path"));
    }
}
