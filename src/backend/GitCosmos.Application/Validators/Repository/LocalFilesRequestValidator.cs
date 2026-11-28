using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Validators.Repository;
public class LocalFilesRequestValidator : AbstractValidator<LocalFilesRequest>
{
    public LocalFilesRequestValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.Paths).Must(paths => paths is { Length: > 0 and <= 500 } && paths.Distinct().Count() == paths.Length && paths.All(path => !string.IsNullOrWhiteSpace(path) && path.Length <= 2000 && !path.Any(char.IsControl))).WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.Area).Must(area => area is "working" or "staged").WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.Action).Must(action => action is "info" or "patch" or "history" or "lineHistory" or "blame" or "discard" or "stash" or "ignore" or "open" or "reveal" or "diff" or "line").WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.Token).Must(token => token is not null && (token.Length == 0 || token.Length == 64 && token.All(Uri.IsHexDigit))).WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.Tool).Must(tool => tool is not null && tool.Length <= 40).WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x.Pattern).Must(value => value is "file" or "extension" or "folder").WithMessage(_ => LocalizedText.Get("local.invalid"));
        RuleFor(x => x).Must(x => x.Start >= 1 && x.End >= x.Start && x.End <= 1000000).WithMessage(_ => LocalizedText.Get("local.invalid"));
    }
}
