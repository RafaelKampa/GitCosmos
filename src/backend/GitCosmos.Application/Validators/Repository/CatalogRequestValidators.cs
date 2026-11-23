using FluentValidation;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Validators.Repository;

internal sealed class CatalogAddValidator : AbstractValidator<CatalogAddRequest>
{
    public CatalogAddValidator() => RuleFor(x => x.LocalPath).Cascade(CascadeMode.Stop)
        .NotEmpty().WithMessage(_ => LocalizedText.Get("repository.path_required"))
        .MaximumLength(32767).WithMessage(_ => LocalizedText.Get("repository.invalid_path"));
}
internal sealed class CatalogCloneValidator : AbstractValidator<CatalogCloneRequest>
{
    public CatalogCloneValidator()
    {
        RuleFor(x => x.RemoteUrl).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(_ => LocalizedText.Get("clone.invalid_url"))
            .MaximumLength(4096).WithMessage(_ => LocalizedText.Get("clone.invalid_url"));
        RuleFor(x => x.ParentPath).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(_ => LocalizedText.Get("repository.path_required"))
            .MaximumLength(32767).WithMessage(_ => LocalizedText.Get("repository.invalid_path"));
    }
}
