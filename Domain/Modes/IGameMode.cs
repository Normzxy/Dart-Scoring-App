namespace Domain.Modes;
using ValueObjects;

public interface IGameMode
{
    /// <summary>
    /// Creates a specific initial score state according to the specific game mode.
    /// </summary>
    PlayerScore CreateInitialScore(Guid playerId);
    
    /// <summary>
    /// Evaluates the result of a throw according to the specific game mode.
    /// </summary>
    ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        PlayerScore playerScore,
        ThrowData throwData);
}