namespace Domain.ValueObjects;
using Modes;

public enum ThrowOutcome
{
    Continue,
    Bust,
    Win
}

/// <summary>
/// Returns information about the result of the throw, based on the current game state.
/// </summary>
public sealed record ThrowEvaluationResult
{
    public ThrowOutcome Outcome { get; init; }
    public PlayerScore UpdatedScore { get; init; } = null!;
    // Optional, because those are edited only in specific situations.
    public IReadOnlyDictionary<Guid, PlayerScore>? OtherUpdatedStates { get; init; }
    
    public static ThrowEvaluationResult Continue(
        PlayerScore updatedScore,
        IReadOnlyDictionary<Guid, PlayerScore>? othersScore = null) => new() 
    { 
        Outcome = ThrowOutcome.Continue, 
        UpdatedScore = updatedScore, 
        OtherUpdatedStates = othersScore
    };
    
    public static ThrowEvaluationResult Bust(
        PlayerScore restoredScore) => new()
    { 
        Outcome = ThrowOutcome.Bust,
        UpdatedScore = restoredScore
    };

    public static ThrowEvaluationResult Win(
        Guid winnerId,
        PlayerScore finalState) => new() 
    { 
        Outcome = ThrowOutcome.Win,
        UpdatedScore = finalState
    };
}