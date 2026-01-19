namespace Domain.Modes.ClassicMode;

public class ClassicModeSettings
{
    public int ScorePerLeg { get; }
    public int LegsToWinMatch { get; }
    public bool DoubleOutEnabled { get; }
    public bool SuddenDeathEnabled { get; }
    public int? SuddenDeathWinningLeg { get; }

    private static readonly int[] AllowedStartingScores = { 201, 301, 401, 501, 601, 701, 1001};

    public ClassicModeSettings(
        int scorePerLeg = 501,
        int legsToWinMatch = 3,
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

        if (legsToWinMatch is < 1 or > 18)
        {
            throw new ArgumentOutOfRangeException(
                nameof(legsToWinMatch),
                legsToWinMatch,
                "Number of legs required to win a match must be between 1 and 18 (inclusive).");
        }

        var effectiveSuddenDeathEnabled = legsToWinMatch > 1 && suddenDeathEnabled;

        if (effectiveSuddenDeathEnabled)
        {
            suddenDeathWinningLeg ??= legsToWinMatch + 2;
            
            if (suddenDeathWinningLeg <= legsToWinMatch)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(suddenDeathWinningLeg),
                    suddenDeathWinningLeg,
                    "Sudden death winning leg must be grater than number of legs to win the match.");
            }
        }
        else
        {
            suddenDeathWinningLeg = null;
        }

        ScorePerLeg = scorePerLeg;
        LegsToWinMatch = legsToWinMatch;
        DoubleOutEnabled = doubleOutEnabled;
        SuddenDeathEnabled = effectiveSuddenDeathEnabled;
        SuddenDeathWinningLeg = suddenDeathWinningLeg;
    }
}