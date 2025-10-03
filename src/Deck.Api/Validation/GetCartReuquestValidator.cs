using Deck.Api.DTOs;
using FluentValidation;

namespace Deck.Api.Validation;

public class GetCartRequestValidator : AbstractValidator<GetCartRequest>
{
    public GetCartRequestValidator()
    {
        // User id should not be less or equal to 0
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
