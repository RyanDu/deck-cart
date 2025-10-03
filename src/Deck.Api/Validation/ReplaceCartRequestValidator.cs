using Deck.Api.DTOs;
using FluentValidation;

namespace Deck.Api.Validation;

public class ReplaceCartRequestValidator : AbstractValidator<ReplaceCartRequest>
{
    public ReplaceCartRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Cart).NotNull();
        RuleForEach(x => x.Cart).ChildRules(child =>
        {
            child.RuleFor(i => i.ItemId).GreaterThan(0);
        });
        RuleFor(x => x.Cart)
            .Must(list => list.Select(i => i.ItemId).Distinct().Count() == list.Count)
            .WithMessage("Duplicate ItemId not allowed");
        RuleFor(x => x.Cart.Count)
            .LessThanOrEqualTo(100) // Add cart limit
            .WithMessage("Too many items");
    }
}
