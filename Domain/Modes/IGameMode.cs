namespace Domain.Modes;
using ValueObjects;

public interface IGameMode
{
    /// <summary>
    /// Starting score for specific game mode.
    /// </summary>
    int GetStartingScore();

    /// <summary>
    /// Evaluates throw based on a specific game mode.
    /// </summary>
    ThrowEvaluationResult EvaluateThrow(
        Guid playerId,
        int currentScore,
        ThrowData throwData);
}