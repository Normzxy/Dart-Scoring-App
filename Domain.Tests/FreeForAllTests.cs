using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;
using Domain.Modes.FreeForAll;

namespace Domain.Tests;

public class FreeForAllTests
{
    private static (Game game, FreeForAll mode, List<Player> players, Dictionary<Guid, PlayerScore> allScores)
        Setup(
            int dartsPerTurn = 1,
            int playerCount = 2,
            int scorePerLeg = 201,
            int legsToWinMatch = 3,
            bool doubleOutEnabled = false)
    {
        var settings = new FreeForAllSettings(
            dartsPerTurn: dartsPerTurn,
            scorePerLeg: scorePerLeg,
            legsToWinMatch: legsToWinMatch,
            doubleOutEnabled: doubleOutEnabled
        );

        var mode = new FreeForAll(settings);

        var players = new List<Player>();
        var allScores = new Dictionary<Guid, PlayerScore>();

        for (var i = 0; i < playerCount; i++)
        {
            var player = new Player($"Player_{i + 1}");
            players.Add(player);
            allScores[player.Id] = mode.CreateInitialScore(player.Id);
        }
            
        var game = new Game(mode, players);

        return (game, mode, players, allScores);
    }

    private static void AssertCannotThrow(Game game, Guid playerId)
    {
        Assert.Throws<InvalidOperationException>(() => 
            game.RegisterThrow(playerId, new ThrowData(1, 1)));
    }

    [Fact]
    public void ValidatePlayers_throws_when_less_than_two_players()
    {
        var settings = new FreeForAllSettings();
        var mode = new FreeForAll(settings);
        var players = new List<Player> { new Player("P1") };

        Assert.Throws<ArgumentException>(() => mode.ValidatePlayers(players));
    }

    [Fact]
    public void ValidatePlayers_throws_when_more_than_four_players()
    {
        var settings = new FreeForAllSettings();
        var mode = new FreeForAll(settings);
        var players = new List<Player> 
        { 
            new Player("P1"), 
            new Player("P2"), 
            new Player("P3"), 
            new Player("P4"), 
            new Player("P5") 
        };

        Assert.Throws<ArgumentException>(() => mode.ValidatePlayers(players));
    }

    [Fact]
    public void ValidatePlayers_accepts_four_players()
    {
        var settings = new FreeForAllSettings();
        var mode = new FreeForAll(settings);
        var players = new List<Player> 
        { 
            new Player("P1"), 
            new Player("P2"), 
            new Player("P3"), 
            new Player("P4") 
        };

        mode.ValidatePlayers(players);
    }

    [Fact]
    public void Bust_when_negative_score()
    {
        var (_, mode, players, allScores) 
        = Setup();
        var player = players[0];
        var score = (ClassicLegsScore)allScores[player.Id];
        allScores[player.Id] = score with { RemainingInLeg = 50 };

        var dart = new ThrowData(17, 3);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
        Assert.Equal(ProggressInfo.None, result.Proggress);
    }

    [Fact]
    public void Bust_when_leaving_one_in_doubleout_mode()
    {
        var (_, mode, players, allScores)
            = Setup(doubleOutEnabled: true);
        var player = players[0];
        var score = (ClassicLegsScore)allScores[player.Id];
        allScores[player.Id] = score with { RemainingInLeg = 52 };

        var dart = new ThrowData(17, 3);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
    }

    [Fact]
    public void Bust_when_zero_but_not_double_in_doubleout_mode()
    {
        var (_, mode, players, allScores)
            = Setup(doubleOutEnabled: true);
        var player = players[0];
        var score = (ClassicLegsScore)allScores[player.Id];
        allScores[player.Id] = score with { RemainingInLeg = 20 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Bust, result.Outcome);
    }

    [Fact]
    public void Normal_subtraction_decreases_remaining()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player = players[0];

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);

        Assert.Equal(201 - 60, updated.RemainingInLeg);
        Assert.Equal(0, updated.LegsWonInMatch);
        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_single_when_double_out_disabled()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player = players[0];
        var score = (ClassicLegsScore)allScores[player.Id];
        allScores[player.Id] = score with { RemainingInLeg = 20 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);

        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(1, updated.LegsWonInMatch);
        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_double_when_double_out_enabled()
    {
        var (_, mode, players, allScores)
            = Setup(doubleOutEnabled: true);
        var player = players[0];
        var score = (ClassicLegsScore)allScores[player.Id];
        allScores[player.Id] = score with { RemainingInLeg = 20 };

        var dart = new ThrowData(10, 2);
        var result = mode.EvaluateThrow(player.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);

        Assert.Equal(201, updated.RemainingInLeg);
        Assert.Equal(1, updated.LegsWonInMatch);
        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_leg_resets_all_opponents_remaining_score()
    {
        var (_, mode, players, allScores)
            = Setup(playerCount: 3);
        var player1 = players[0];
        var player2 = players[1];
        var player3 = players[2];
        var score1 = (ClassicLegsScore)allScores[player1.Id];
        var score2 = (ClassicLegsScore)allScores[player2.Id];
        var score3 = (ClassicLegsScore)allScores[player3.Id];

        allScores[player1.Id] = score1 with { RemainingInLeg = 20 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 180 };
        allScores[player3.Id] = score3 with { RemainingInLeg = 100 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.NotNull(result.OtherUpdatedScores);
        var score2Updated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];
        var score3Updated = (ClassicLegsScore)result.OtherUpdatedScores[player3.Id];

        // Second player
        Assert.Equal(201, score2Updated.RemainingInLeg);
        Assert.Equal(0, score2Updated.LegsWonInMatch);
        // Third player
        Assert.Equal(201, score3Updated.RemainingInLeg);
        Assert.Equal(0, score3Updated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.LegWon, result.Proggress);
        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Win_match_in_thee_player_game()
    {
        var (_, mode, players, allScores)
            = Setup(playerCount: 3);
        var player1 = players[0];
        var player2 = players[1];
        var player3 = players[2];
        var score1 = (ClassicLegsScore)allScores[player1.Id];
        var score2 = (ClassicLegsScore)allScores[player2.Id];
        var score3 = (ClassicLegsScore)allScores[player3.Id];
        allScores[player1.Id] = score1 with { RemainingInLeg = 20, LegsWonInMatch = 2 };
        allScores[player2.Id] = score2 with { RemainingInLeg = 100, LegsWonInMatch = 1 };
        allScores[player3.Id] = score3 with { RemainingInLeg = 150, LegsWonInMatch = 0 };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<ClassicLegsScore>(result.UpdatedScore);
        Assert.NotNull(result.OtherUpdatedScores);
        var score2Updated = (ClassicLegsScore)result.OtherUpdatedScores[player2.Id];
        var score3Updated = (ClassicLegsScore)result.OtherUpdatedScores[player3.Id];

        //First player (state changes).
        Assert.Equal(3, updated.LegsWonInMatch);
        Assert.Equal(201, updated.RemainingInLeg);
        // Second player (state does not change).
        Assert.Equal(100, score2Updated.RemainingInLeg);
        Assert.Equal(1, score2Updated.LegsWonInMatch);
        // Third player (state does not change).
        Assert.Equal(150, score3Updated.RemainingInLeg);
        Assert.Equal(0, score3Updated.LegsWonInMatch);

        Assert.Equal(ProggressInfo.None, result.Proggress);
        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Workflow_three_players()
    {
        var (game, _, players, _)
            = Setup(legsToWinMatch: 2, scorePerLeg: 101, playerCount: 3);

        var player1 = players[0];
        var player2 = players[1];
        var player3 = players[2];

        game.RegisterThrow(player1.Id, new ThrowData(16, 1)); // (P1) 0:85
        AssertCannotThrow(game, player1.Id);
        game.RegisterThrow(player2.Id, new ThrowData(15, 1)); // (P2) 0:86
        AssertCannotThrow(game, player2.Id);
        game.RegisterThrow(player3.Id, new ThrowData(20, 3)); // (P3) 0:41
        AssertCannotThrow(game, player3.Id);

        game.RegisterThrow(player1.Id, new ThrowData(15, 1)); // (P1) 0:70
        game.RegisterThrow(player2.Id, new ThrowData(16, 1)); // (P2) 0:70
        game.RegisterThrow(player3.Id, new ThrowData(7, 3)); // (P3) 0:20

        game.RegisterThrow(player1.Id, new ThrowData(20, 1)); // (P1) 0:50
        game.RegisterThrow(player2.Id, new ThrowData(20, 2)); // (P2) 0:30
        // P3 turn (wins leg)
        var r1 = game.RegisterThrow(player3.Id, new ThrowData(20, 1)); // (P1) 1:101
        Assert.Equal(ProggressInfo.LegWon, r1.Proggress);
        AssertCannotThrow(game, player1.Id);

        // P2 turn (starts leg)
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 0:41
        game.RegisterThrow(player3.Id, new ThrowData(20, 3)); // (P3) 1:41
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:41
        game.RegisterThrow(player2.Id, new ThrowData(7, 3)); // (P2) 0:20
        game.RegisterThrow(player3.Id, new ThrowData(7, 1)); // (P3) 1:34
        game.RegisterThrow(player1.Id, new ThrowData(7, 2)); // (P1) 0:27

        // P2 turn (wins leg)
        var r2 = game.RegisterThrow(player2.Id, new ThrowData(20, 1)); // (P2) 1:101
        Assert.Equal(ProggressInfo.LegWon, r2.Proggress);
        AssertCannotThrow(game, player1.Id);
        AssertCannotThrow(game, player2.Id);

        // P3 turn (starts leg)
        game.RegisterThrow(player3.Id, new ThrowData(20, 3)); // (P3) 1:41
        game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:41
        game.RegisterThrow(player2.Id, new ThrowData(20, 3)); // (P2) 1:41
        game.RegisterThrow(player3.Id, new ThrowData(7, 3)); // (P3) 1:20

        // P1 turn (busts)
        var r3 = game.RegisterThrow(player1.Id, new ThrowData(20, 3)); // (P1) 0:41
        Assert.Equal(ProggressInfo.None, r3.Proggress);

        game.RegisterThrow(player2.Id, new ThrowData(7, 1)); // (P2) 1:34

        // P3 turn (wins game)
        var r4 = game.RegisterThrow(player3.Id, new ThrowData(20, 1)); // (P3) 2:101
        Assert.Equal(ProggressInfo.None, r4.Proggress);

        // Verify final scores
        var p1Score = Assert.IsType<ClassicLegsScore>(game.ScoreStates[player1.Id]);
        var p2Score = Assert.IsType<ClassicLegsScore>(game.ScoreStates[player2.Id]);
        var p3Score = Assert.IsType<ClassicLegsScore>(game.ScoreStates[player3.Id]);

        // P1
        Assert.Equal(0, p1Score.LegsWonInMatch);
        Assert.Equal(41, p1Score.RemainingInLeg);
        // P2
        Assert.Equal(1, p2Score.LegsWonInMatch);
        Assert.Equal(34, p2Score.RemainingInLeg);
        // P3
        Assert.Equal(2, p3Score.LegsWonInMatch);
        Assert.Equal(101, p3Score.RemainingInLeg);
    }
}