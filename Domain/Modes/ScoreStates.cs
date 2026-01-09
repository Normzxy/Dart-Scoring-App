namespace Domain.Modes;

/// <summary>
/// Stores score structure according to the specific game mode.
/// </summary>
public abstract record PlayerScore
{
    public Guid PlayerId { get; init; }
}

public sealed record ClassicSetsPlayerScore : PlayerScore
{
    public int RemainingInLeg { get; init; }
    public int LegsWonInSet { get; init; }
    public int SetsWonInMatch { get; init; }
}