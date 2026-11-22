using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Domain.Common;

namespace GitCosmos.Application.Validators.Workspace;

public class WorkspacePaginationValidator : AbstractValidator<PaginationRequest>
{
    public WorkspacePaginationValidator()
    {
        RuleFor(x => x.GetPage()).InclusiveBetween(0, int.MaxValue / PaginationRequest.MaxPageSize)
            .WithMessage(_ => LocalizedText.Get("workspace.pagination"));
        RuleFor(x => x.GetPageSize()).InclusiveBetween(PaginationRequest.MinPageSize, PaginationRequest.MaxPageSize)
            .WithMessage(_ => LocalizedText.Get("workspace.pagination"));
    }
}

