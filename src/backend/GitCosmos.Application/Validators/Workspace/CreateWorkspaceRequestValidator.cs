using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;

namespace GitCosmos.Application.Validators.Workspace;

public class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(_ => LocalizedText.Get("workspace.name_required"))
            .Must(name => name.Trim().Length <= 100).WithMessage(_ => LocalizedText.Get("workspace.name_length"));
    }
}

