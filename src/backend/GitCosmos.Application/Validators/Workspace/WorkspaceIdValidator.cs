using FluentValidation;
using GitCosmos.Application.Common.Localization;

namespace GitCosmos.Application.Validators.Workspace;

public class WorkspaceIdValidator : AbstractValidator<int>
{
    public WorkspaceIdValidator()
    {
        RuleFor(id => id).GreaterThan(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
    }
}
