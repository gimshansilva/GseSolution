using FluentValidation;
using Gse.Api.Domain;
using Gse.Api.Models;

namespace Gse.Api.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(x => x.CategoryCode).NotEmpty().MaximumLength(10).Matches("^[A-Z0-9]+$")
            .WithMessage("CategoryCode must be upper-case letters and digits.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(250);
    }
}

public class CreateAssetValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetValidator()
    {
        RuleFor(x => x.AssetTag).NotEmpty().MaximumLength(20).Matches("^[A-Z0-9-]+$")
            .WithMessage("AssetTag must be upper-case letters, digits and hyphens.");
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.TotalOperatingHours).InclusiveBetween(0, 999_999.99m);
    }
}

public class UpdateStatusValidator : AbstractValidator<UpdateStatusRequest>
{
    public UpdateStatusValidator() =>
        RuleFor(x => x.OperationalStatus).Must(s => AssetStatuses.All.Contains(s))
            .WithMessage($"OperationalStatus must be one of: {string.Join(", ", AssetStatuses.All)}.");
}

public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.Priority).Must(p => Priorities.All.Contains(p))
            .WithMessage($"Priority must be one of: {string.Join(", ", Priorities.All)}.");
        RuleFor(x => x.IssueDescription).NotEmpty().MaximumLength(500);
    }
}

public class ResolveOrderValidator : AbstractValidator<ResolveOrderRequest>
{
    public ResolveOrderValidator() =>
        RuleFor(x => x.ResolutionRemarks).NotEmpty().MaximumLength(500);
}
