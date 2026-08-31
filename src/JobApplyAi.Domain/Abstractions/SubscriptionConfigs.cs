namespace JobApplyAi.Domain.Abstractions;

// Deserialized shapes of JobSourceSubscription.ConfigJson, one per source.

public sealed record GreenhouseSubscriptionConfig(string BoardToken);

public sealed record LeverSubscriptionConfig(string Company);

public sealed record AdzunaSubscriptionConfig(string Keywords, string? Location, string Country);
