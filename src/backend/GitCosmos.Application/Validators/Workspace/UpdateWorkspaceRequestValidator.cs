using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;

namespace GitCosmos.Application.Validators.Workspace;

public class UpdateWorkspaceRequestValidator : AbstractValidator<UpdateWorkspaceRequest>
{
    public UpdateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => LocalizedText.Get("workspace.name_required"))
            .Must(name => name.Trim().Length <= 100).WithMessage(_ => LocalizedText.Get("workspace.name_length"));
    }
}
