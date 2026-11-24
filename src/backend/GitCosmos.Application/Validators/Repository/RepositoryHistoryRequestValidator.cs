using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Validators.Repository;

public class RepositoryHistoryRequestValidator : AbstractValidator<RepositoryHistoryRequest>
{
    public RepositoryHistoryRequestValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("repository.invalid_id"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.Page).InclusiveBetween(0, 10000).WithMessage(_ => LocalizedText.Get("history.invalid"));
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(_ => LocalizedText.Get("history.invalid"));
        RuleFor(x => x.Snapshot).Must((request, value) =>
            value is null ? request.Page == 0 : value.Length == 64 && value.All(Uri.IsHexDigit))
            .WithMessage(_ => LocalizedText.Get("history.invalid"));
    }
}

public class RepositoryCommitRequestValidator : AbstractValidator<RepositoryCommitRequest>
{
    public RepositoryCommitRequestValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("repository.invalid_id"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        RuleFor(x => x.Sha).Must(value => value is not null && value.Length is 40 or 64 && value.All(Uri.IsHexDigit))
            .WithMessage(_ => LocalizedText.Get("history.invalid"));
    }
}
