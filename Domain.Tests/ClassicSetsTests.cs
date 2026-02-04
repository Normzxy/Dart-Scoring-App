using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;
using Domain.Modes.ClassicSets;

namespace Domain.Tests;

public class ClassicSetsTests
{
    private static (Game game, ClassicSets mode, Player player1, Player player2, 
        ClassicSetsScore score1, ClassicSetsScore score2, Dictionary<Guid, PlayerScore> allScores)
        Setup
        (int scorePerLeg = 201,
            bool doubleOutEnabled = false,
            bool advantagesEnabled = false,
            int setsToWinMatch = 3,
            int suddenDeathWinningLeg = 6)
    {
        var settings = new ClassicSetsSettings(
            scorePerLeg: scorePerLeg,
            doubleOutEnabled: doubleOutEnabled,
            advantagesEnabled: advantagesEnabled,
            setsToWinMatch: setsToWinMatch,
            suddenDeathWinningLeg: suddenDeathWinningLeg
        );

        var mode = new ClassicSets(settings);

        var player1 = new Player("P1");
        var player2 = new Player("P2");
        var players = new List<Player> { player1, player2 };

        var score1 = (ClassicSetsScore)mode.CreateInitialScore(player1.Id);
        var score2 = (ClassicSetsScore)mode.CreateInitialScore(player2.Id);
        var allScores = new Dictionary<Guid, PlayerScore>
        {
            [player1.Id] = score1,
            [player2.Id] = score2
        };

        var game = new Game(mode, players);

        return (game, mode, player1, player2, score1, score2, allScores);
    }

    private static void AssertCannotThrow(Game game, Guid playerId)
    {
        Assert.Throws<InvalidOperationException>(
            () => game.RegisterThrow(playerId, new ThrowData(20, 1)));
    }

    [Fact]
    public void ValidatePlayers_throws_when_not_exactly_two_players()
    {
        var settings = new ClassicSetsSettings();
        var mode = new ClassicSets(settings);
        var players = new List<Player> { new Player("P1") };

        Assert.Throws<InvalidOperationException>(() => mode.ValidatePlayers(players));
    }

    [Fact]
    public void Bust_when_negative_score()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 50, LegsWonInSet = 0, SetsWonInMatch = 0 };

        var dart = new ThrowData(17, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
    }

    [Fact]
    public void Bust_when_leaving_one_in_doubleout_mode()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup(doubleOutEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 52, LegsWonInSet = 0, SetsWonInMatch = 0 };

        var dart = new ThrowData(17, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
    }

    [Fact]
    public void Bust_when_zero_but_not_double_in_doubleout_mode()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup(doubleOutEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
    }

    [Fact]
    public void Normal_subtraction_decreases_remaining()
    {
        var (_, mode, player1, _, _, _, allScores)
            = Setup();

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);

        Assert.Equal(201 - 60, updated.RemainingInLeg);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_single_when_double_out_disabled()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);

        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(1, updated.LegsWonInSet);
        Assert.Equal(0, updated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_double_when_double_out_enabled()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup(doubleOutEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 0, SetsWonInMatch = 0 };

        var dart = new ThrowData(10, 2);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);

        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(1, updated.LegsWonInSet);
        Assert.Equal(0, updated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_set_resets__opponent_remaining_score_and_increments_sets()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 0 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[player2.Id];

        //First player.
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(0, updated.LegsWonInSet);
        Assert.Equal(1, updated.SetsWonInMatch);
        // Second player.
        Assert.Equal(201, otherUpdated.RemainingInLeg);
        Assert.Equal(0, otherUpdated.LegsWonInSet);
        Assert.Equal(0, otherUpdated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.SetWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_match_normal_flow()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 10, LegsWonInSet = 1, SetsWonInMatch = 0 };

        var dart = new ThrowData(20, 1); // Finishes match.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var opponentUpdated = (ClassicSetsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(0, updated.LegsWonInSet);
        Assert.Equal(3, updated.SetsWonInMatch);
        // Second player (state does not change).
        Assert.Equal(10, opponentUpdated.RemainingInLeg);
        Assert.Equal(1, opponentUpdated.LegsWonInSet);
        Assert.Equal(0, opponentUpdated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }
        
    // Minimum 2 legs ahead required to win decider.
    [Fact]
    public void Decider_continues_because_required_advantage_not_reached()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup(advantagesEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 2, SetsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };

        var dart = new ThrowData(20, 1); // Wins 3rd leg, but needs 2 legs advantage.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(3, updated.LegsWonInSet);
        Assert.Equal(2, updated.SetsWonInMatch);
        // Second player (state changes).
        Assert.Equal(201, otherUpdated.RemainingInLeg);
        Assert.Equal(2, otherUpdated.LegsWonInSet);
        Assert.Equal(2, otherUpdated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Decider_win_by_two_legs_on_advantages_enabled()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup(advantagesEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 3, SetsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 30, LegsWonInSet = 2, SetsWonInMatch = 2 };

        var dart = new ThrowData(20, 1); // Advantage reached.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(0, updated.LegsWonInSet);
        Assert.Equal(3, updated.SetsWonInMatch);
        // Second player (state does not change).
        Assert.Equal(30, otherUpdated.RemainingInLeg);
        Assert.Equal(2, otherUpdated.LegsWonInSet);
        Assert.Equal(2, otherUpdated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Decider_win_by_sudden_death()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup(advantagesEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInSet = 5, SetsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 30, LegsWonInSet = 5, SetsWonInMatch = 2 };

        var dart = new ThrowData(20, 1); // Reaches sudden death leg.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicSetsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicSetsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(0, updated.LegsWonInSet);
        Assert.Equal(3, updated.SetsWonInMatch);
        // Second player (state does not change).
        Assert.Equal(30, otherUpdated.RemainingInLeg);
        Assert.Equal(5, otherUpdated.LegsWonInSet);
        Assert.Equal(2, otherUpdated.SetsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Workflow()
    {
        var (game, _, player1, player2, _, _, _)
            = Setup(doubleOutEnabled:true);

        // P1 turn (starts set)
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:0:21

        // P2 turn
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:0:21

        // P1 turn (bust)
        game.RegisterThrow(player1.Id, new ThrowData(7, 3)); // (P1) 0:0:21

        // P2 turn (wins leg)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(1, 1));
        var r1 = game.RegisterThrow(player2.Id, new ThrowData(10, 2)); // (P2) 0:1:201
        Assert.Equal(ProggressInfo.LegWon, r1.Proggress);

        // P2 turn (starts leg)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:1:21

        // P1 turn
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:0:21

        // P2 turn (wins leg)
        game.RegisterThrow(player2.Id, new ThrowData(1, 1));
        var r2 = game.RegisterThrow(player2.Id, new ThrowData(10, 2)); // (P2) 0:2:201
        Assert.Equal(ProggressInfo.LegWon, r2.Proggress);

        // P1 turn (starts leg)
        AssertCannotThrow(game, player2.Id);
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:0:21

        // P2 turn
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:2:21

        // P1 turn (wins leg)
        AssertCannotThrow(game, player2.Id);
        game.RegisterThrow(player1.Id, new ThrowData(7, 1));
        var r3 = game.RegisterThrow(player1.Id, new ThrowData(7, 2)); // (P1) 0:1:201
        Assert.Equal(ProggressInfo.LegWon, r3.Proggress);

        // P2 turn (starts leg)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:2:21

        // P1 turn
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:1:21

        // P2 turn (bust)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 1)); // (P2) 0:2:21

        // P1 turn (wins leg)
        AssertCannotThrow(game, player2.Id);
        game.RegisterThrow(player1.Id, new ThrowData(1, 1));
        var r4 = game.RegisterThrow(player1.Id, new ThrowData(10, 2)); // (P1) 0:2:201 -> leg won
        Assert.Equal(ProggressInfo.LegWon, r4.Proggress);

        // Score states check
        var player1Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[player1.Id]);
        var player2Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[player2.Id]);

        // P1
        Assert.Equal(201, player1Score.RemainingInLeg);
        Assert.Equal(2, player1Score.LegsWonInSet);
        Assert.Equal(0, player1Score.SetsWonInMatch);

        // P2
        Assert.Equal(201, player2Score.RemainingInLeg);
        Assert.Equal(2, player2Score.LegsWonInSet);
        Assert.Equal(0, player2Score.SetsWonInMatch);

        // P1 turn (starts leg)
        game.RegisterThrow(player1.Id, new ThrowData(19, 3));
        game.RegisterThrow(player1.Id, new ThrowData(19, 3));
        game.RegisterThrow(player1.Id, new ThrowData(19, 3)); // (P1) 0:2:30

        // P2 turn
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:2:21

        // P1 turn (bust)
        game.RegisterThrow(player1.Id, new ThrowData(10, 2));
        game.RegisterThrow(player1.Id, new ThrowData(5, 1));
        game.RegisterThrow(player1.Id, new ThrowData(20, 1)); // (P1) 0:2:30

        // P2 turn (wins set)
        game.RegisterThrow(player2.Id, new ThrowData(5, 1));
        var r5 = game.RegisterThrow(player2.Id, new ThrowData(8, 2)); // (P2) 1:0:201 -> set won
        Assert.Equal(ProggressInfo.SetWon, r5.Proggress);

        // P2 turn (starts set)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 1:0:21

        // P1 turn
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:0:21

        // P2 turn
        game.RegisterThrow(player2.Id, new ThrowData(1, 1));
        game.RegisterThrow(player2.Id, new ThrowData(10, 1));
        game.RegisterThrow(player2.Id, new ThrowData(0, 1)); // (P2) 1:0:21

        // P1 turn (wins leg)
        game.RegisterThrow(player1.Id, new ThrowData(1, 1));
        var r6 = game.RegisterThrow(player1.Id, new ThrowData(10, 2)); // (P1) 0:1:201 -> leg won
        Assert.Equal(ProggressInfo.LegWon, r6.Proggress);

        // Verify scores
        player1Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[player1.Id]);
        player2Score = Assert.IsType<ClassicSetsScore>(game.ScoreStates[player2.Id]);

        // P1
        Assert.Equal(201, player1Score.RemainingInLeg);
        Assert.Equal(1, player1Score.LegsWonInSet);
        Assert.Equal(0, player1Score.SetsWonInMatch);
        // P2
        Assert.Equal(201, player2Score.RemainingInLeg);
        Assert.Equal(0, player2Score.LegsWonInSet);
        Assert.Equal(1, player2Score.SetsWonInMatch);
    }
}