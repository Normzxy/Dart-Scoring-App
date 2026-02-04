using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;
using Domain.Modes.ClassicLegs;

namespace Domain.Tests;

public class ClassicLegsTests
{
    private static (Game game, ClassicLegs mode, Player player1, Player player2, 
        ClassicLegsScore score1, ClassicLegsScore score2, Dictionary<Guid, PlayerScore> allScores)
        Setup(
            int scorePerLeg = 201,
            bool doubleOutEnabled = false,
            bool advantagesEnabled = false,
            int legsToWinMatch = 3,
            int suddenDeathWinningLeg = 6)
    {
        var settings = new ClassicLegsSettings(
            scorePerLeg: scorePerLeg,
            doubleOutEnabled: doubleOutEnabled,
            advantagesEnabled: advantagesEnabled,
            legsToWinMatch: legsToWinMatch,
            suddenDeathWinningLeg: suddenDeathWinningLeg
        );

        var mode = new ClassicLegs(settings);

        var player1 = new Player("Player_1");
        var player2 = new Player("Player_2");
        var players = new List<Player> { player1, player2 };

        var score1 = (ClassicLegsScore)mode.CreateInitialScore(player1.Id);
        var score2 = (ClassicLegsScore)mode.CreateInitialScore(player2.Id);
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
        Assert.Throws<InvalidOperationException>(() => 
            game.RegisterThrow(playerId, new ThrowData(1, 1)));
    }

    [Fact]
    public void ValidatePlayers_throws_when_not_exactly_two_players()
    {
        var settings = new ClassicLegsSettings();
        var mode = new ClassicLegs(settings);
        var players = new List<Player> { new Player("P1") };

        Assert.Throws<InvalidOperationException>(() => mode.ValidatePlayers(players));
    }

    [Fact]
    public void Bust_when_negative_score()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 50, LegsWonInMatch = 0 };

        var dart = new ThrowData(17, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
    }

    [Fact]
    public void Bust_when_leaving_one_in_doubleout_mode()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup(doubleOutEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 52, LegsWonInMatch = 0 };

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
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 0 };

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

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);

        Assert.Equal(201 - 60, updated.RemainingInLeg);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_single_when_double_out_disabled()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 0 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);

        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(1, updated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_double_when_double_out_enabled()
    {
        var (_, mode, player1, _, score1, _, allScores)
            = Setup(doubleOutEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 0 };

        var dart = new ThrowData(10, 2);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);

        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(1, updated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_resets_opponent_remaining_score()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 0 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 100, LegsWonInMatch = 0 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.NotNull(result.OtherUpdatedScores);
        var opponentUpdated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];

        Assert.Equal(201, opponentUpdated.RemainingInLeg);
        Assert.Equal(0, opponentUpdated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_match_normal_flow()
    {
        var (_, mode, player1, player2, score1, score2, allScores)
            = Setup();
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 10, LegsWonInMatch = 1 };

        var dart = new ThrowData(20, 1); // Finishes match.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var opponentUpdated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(3, updated.LegsWonInMatch);
        // Second player (state does not change).
        Assert.Equal(10, opponentUpdated.RemainingInLeg);
        Assert.Equal(1, opponentUpdated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Continues_because_required_advantage_not_reached()
    {
        var (_, mode, player1, player2, score1, score2, allScores) 
            = Setup(advantagesEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 30, LegsWonInMatch = 2 };

        var dart = new ThrowData(20, 1); // Wins 3rd leg, but needs 2 legs advantage.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(3, updated.LegsWonInMatch);
        // Second player (state changes).
        Assert.Equal(201, otherUpdated.RemainingInLeg);
        Assert.Equal(2, otherUpdated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Match_win_by_two_legs_on_advantages_enabled()
    {
        var (_, mode, player1, player2, score1, score2, allScores) 
            = Setup(advantagesEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 3 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 30, LegsWonInMatch = 2 };

        var dart = new ThrowData(20, 1); // Advantage reached.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(4, updated.LegsWonInMatch);
        // Second player (state does not change).
        Assert.Equal(30, otherUpdated.RemainingInLeg);
        Assert.Equal(2, otherUpdated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Match_win_by_sudden_death()
    {
        var (_, mode, player1, player2, score1, score2, allScores) 
            = Setup(advantagesEnabled: true);
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 5 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 30, LegsWonInMatch = 5 };

        var dart = new ThrowData(20, 1); // Reaches sudden death leg.
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var otherUpdated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];

        //First player (state changes).
        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(6, updated.LegsWonInMatch);
        // Second player (state does not change).
        Assert.Equal(30, otherUpdated.RemainingInLeg);
        Assert.Equal(5, otherUpdated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Workflow()
    {
        var (game, _, player1, player2, _, _, _)
            = Setup(doubleOutEnabled: true);

        // P1 turn (starts leg)
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:21

        // P2 turn
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:21

        // P1 turn (bust)
        game.RegisterThrow(player1.Id, new ThrowData(20, 1));

        // P2 turn (wins leg)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(1, 1));
        var r2 = game.RegisterThrow(player2.Id, new ThrowData(10, 2));
        Assert.Equal(ProggressInfo.LegWon, r2.Proggress); // (P2) 1:201

        // P2 starts (starts leg)
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 1:21

        // P1 turn
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:21
            
        // P2 turn (wins leg)
        game.RegisterThrow(player2.Id, new ThrowData(1, 1));
        var r3 = game.RegisterThrow(player2.Id, new ThrowData(10, 2));
        Assert.Equal(ProggressInfo.LegWon, r3.Proggress); // (P2) 2:201

        // P1 (starts leg)
        AssertCannotThrow(game, player2.Id);
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3));
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:21

        // P2 turn
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3));
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 2:21

        // P1 turn (wins leg)
        game.RegisterThrow(player1.Id, new ThrowData(1, 1));
        var r4 = game.RegisterThrow(player1.Id, new ThrowData(10, 2));
        Assert.Equal(ProggressInfo.LegWon, r4.Proggress); // (P1) 1:201

        // Verify scores
        var p1Score = Assert.IsType<ClassicLegsScore>(game.ScoreStates[player1.Id]);
        var p2Score = Assert.IsType<ClassicLegsScore>(game.ScoreStates[player2.Id]);

        // P1
        Assert.Equal(201, p1Score.RemainingInLeg);
        Assert.Equal(1, p1Score.LegsWonInMatch);
        // P2
        Assert.Equal(201, p2Score.RemainingInLeg);
        Assert.Equal(2, p2Score.LegsWonInMatch);
    }
}