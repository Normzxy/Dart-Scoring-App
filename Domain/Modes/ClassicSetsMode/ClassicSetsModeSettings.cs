namespace Domain.Modes.ClassicSetsMode;

public sealed class ClassicSetsModeSettings
{
    public int ScorePerLeg { get; }
    public int LegsToWinSet { get; }
    public int SetsToWinMatch { get; }
    public bool DoubleOutEnabled { get; }
    public bool SuddenDeathEnabled { get; }
    public int? SuddenDeathWinningLeg { get; }

    private static readonly int[] AllowedStartingScores = { 201, 301, 401, 501, 701 };

    public ClassicSetsModeSettings(
        int scorePerLeg = 501,
        int legsToWinSet = 3,
        int setsToWinMatch = 3,
        bool doubleOutEnabled = false,
        bool suddenDeathEnabled = false,
        int? suddenDeathWinningLeg = null)
    {
        if (!AllowedStartingScores.Contains(scorePerLeg))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scorePerLeg),
                scorePerLeg,
                $"Score per leg must be one of: {string.Join(", ", AllowedStartingScores)}.");
        }

        if (setsToWinMatch is < 3 or > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(setsToWinMatch),
                setsToWinMatch,
                "Number of sets required to win the match must be between 3 and 7 (inclusive).");
        }

        if (legsToWinSet is < 2 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(legsToWinSet),
                legsToWinSet,
                "Number of legs required to win a set must be between 2 and 4 (inclusive).");
        }

        if (suddenDeathEnabled)
        {
            suddenDeathWinningLeg ??= legsToWinSet + 2;

            if (suddenDeathWinningLeg <= legsToWinSet)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(suddenDeathWinningLeg),
                    suddenDeathWinningLeg,
                    "Sudden death winning leg must be greater than number of legs required to win a set.");
            }
        }
        else
        {
            suddenDeathWinningLeg = null;
        }

        ScorePerLeg = scorePerLeg;
        LegsToWinSet = legsToWinSet;
        SetsToWinMatch = setsToWinMatch;
        DoubleOutEnabled = doubleOutEnabled;
        SuddenDeathEnabled = suddenDeathEnabled;
        SuddenDeathWinningLeg = suddenDeathWinningLeg;
    }
}