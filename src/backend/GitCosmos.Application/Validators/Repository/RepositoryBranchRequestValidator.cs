using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Validators.Repository;

public class RepositoryBranchQueryValidator : AbstractValidator<RepositoryBranchQuery>
{
    public RepositoryBranchQueryValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("repository.invalid_id"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
    }
}
public class RepositoryBranchRequestValidator : AbstractValidator<RepositoryBranchRequest>
{
    public RepositoryBranchRequestValidator()
    {
        RuleFor(x => x).SetValidator(new BranchScopeValidator());
        RuleFor(x => x.Operation).Must(value => value is "create" or "rename" or "delete" or "checkout" or "worktree" or "fastForward" or "push" or "pullRequest" or "merge" or "rebase" or "interactiveRebase" or "tag" or "setUpstream" or "unsetUpstream" or "fetch" or "checkoutRemote" or "deleteRemote" or "pull" or "continue" or "abort").WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Name).Must(ValidName).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.NewName).Must(ValidName).When(x => x.Operation is "create" or "rename").WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.NewName).Equal("").When(x => x.Operation is "delete" or "checkout").WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.NewName).Must(ValidName).When(x => x.Operation is "tag" or "checkoutRemote" || x.Operation == "worktree" && x.IsRemote).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Remote).Must(value => value is not null && value.Length <= 255 && !value.StartsWith('-') && !value.Any(char.IsControl)).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.RemoteBranch).Must(value => value is not null && value.Length <= 255 && !value.Any(char.IsControl)).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Upstream).Must(value => value is not null && value.Length <= 511 && !value.Any(char.IsControl)).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.BaseBranch).Must(value => value is not null && value.Length <= 255 && !value.Any(char.IsControl)).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Destination).Must(value => value is not null && value.Length <= 3000 && !value.Any(char.IsControl)).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Message).Must(value => value is not null && value.Length <= 10000 && !value.Contains('\0')).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Confirmation).Must(value => value is not null && value.Length <= 511 && !value.Any(char.IsControl)).WithMessage(_ => LocalizedText.Get("branch.invalid"));
        RuleFor(x => x.Steps).Must(value => value is null || value.Length <= 200 && value.All(step => step is not null && step.Sha is not null && step.Sha.Length is 40 or 64 && step.Sha.All(Uri.IsHexDigit) && step.Action is "pick" or "drop" or "squash" or "fixup")).WithMessage(_ => LocalizedText.Get("branch.invalid"));
    }
    private static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 255 && !name.StartsWith('-') && name != "HEAD" && !name.Any(char.IsControl);
    private sealed class BranchScopeValidator : AbstractValidator<RepositoryBranchRequest>
    {
        public BranchScopeValidator()
        {
            RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("repository.invalid_id"));
            RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("workspace.invalid_id"));
        }
    }
}
