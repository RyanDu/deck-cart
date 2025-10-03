using System.Diagnostics;

namespace Deck.Api.Telemetry;

public static class Tracing
{
    public static readonly ActivitySource Source = new("deck-cart");
}
