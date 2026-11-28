using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Validators.Repository;
public class RepositoryLinesRequestValidator : AbstractValidator<RepositoryLinesRequest>
{
    public RepositoryLinesRequestValidator()
    {
        RuleFor(x => x.RepositoryId).GreaterThan(0).WithMessage(_ => LocalizedText.Get("lines.invalid"));
        RuleFor(x => x.WorkspaceId).GreaterThanOrEqualTo(0).WithMessage(_ => LocalizedText.Get("lines.invalid"));
        RuleFor(x => x.Area).Must(value => value is "working" or "staged").WithMessage(_ => LocalizedText.Get("lines.invalid"));
        RuleFor(x => x.Path).Must(value => !string.IsNullOrEmpty(value) && value.Length <= 2000 && !value.Contains('\0')).WithMessage(_ => LocalizedText.Get("lines.invalid"));
    }
}
public class PrepareLinesRequestValidator : AbstractValidator<PrepareLinesRequest>
{
    public PrepareLinesRequestValidator()
    {
        RuleFor(x => x).Must(x => x.Operation == "move" || x.Operation == "discard" && x.File?.Area == "working").WithMessage(_ => LocalizedText.Get("lines.invalid"));
        RuleFor(x => x.File).NotNull().WithMessage(_ => LocalizedText.Get("lines.invalid"));
        RuleFor(x => x.File).SetValidator(new RepositoryLinesRequestValidator());
        RuleFor(x => x.Lines).Must(value => value is { Length: > 0 and <= 4000 } && value.All(line => line >= 0 && line < 10000) && value.Distinct().Count() == value.Length).WithMessage(_ => LocalizedText.Get("lines.invalid"));
        RuleFor(x => x.Token).Must(value => value is { Length: 64 } && value.All(Uri.IsHexDigit)).WithMessage(_ => LocalizedText.Get("lines.invalid"));
    }
}
