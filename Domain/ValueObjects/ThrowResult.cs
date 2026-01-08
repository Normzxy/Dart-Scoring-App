namespace Domain.ValueObjects;

public enum ThrowOutcome
{
    Continue,
    Bust,
    Win
}

public sealed record ThrowEvaluationResult
{
    public ThrowOutcome Outcome { get; }
    public int UpdatedScoreValue { get; }
    public Guid? WinnerId { get; }

    private ThrowEvaluationResult(
        ThrowOutcome outcome,
        int updatedScoreValue,
        Guid? winnerId = null)
    {
        Outcome = outcome;
        UpdatedScoreValue = updatedScoreValue;
        WinnerId = winnerId;
    }

    public static ThrowEvaluationResult Continue(int newScore) =>
        new(ThrowOutcome.Continue, newScore);

    public static ThrowEvaluationResult Bust(int restoredScore) =>
        new(ThrowOutcome.Bust, restoredScore);

    public static ThrowEvaluationResult Win(Guid winnerId, int finalScore) =>
        new(ThrowOutcome.Win, finalScore, winnerId);
}