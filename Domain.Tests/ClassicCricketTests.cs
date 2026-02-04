using Domain.Modes;
using Domain.ValueObjects;
using Domain.Entities;
using Domain.Modes.ClassicCricket;

namespace Domain.Tests;

public class ClassicCricketTests
{
    private static (Game game, ClassicCricket mode, List<Player> players, Dictionary<Guid, PlayerScore> allScores)
        Setup(
            int dartsPerTurn = 3,
            int playerCount = 2,
            int hitsToCloseSector = 3,
            bool countMultipliers = true)
    {
        var settings = new ClassicCricketSettings(
            dartsPerTurn: dartsPerTurn,
            hitsToCloseSector: hitsToCloseSector,
            countMultipliers: countMultipliers
        );

        var mode = new ClassicCricket(settings);

        var players = new List<Player>();
        var allScores = new Dictionary<Guid, PlayerScore>();

        for (var i = 1; i <= playerCount; i++)
        {
            var player = new Player($"P_{i + 1}");
            players.Add(player);
            allScores[player.Id] = mode.CreateInitialScore(player.Id);
        }

        var game = new Game(mode, players);

        return (game, mode, players, allScores);
    }

    private static void AssertCannotThrow(Game game, Guid playerId)
    {
        Assert.Throws<InvalidOperationException>(
            () => game.RegisterThrow(playerId, new ThrowData(20, 1)));
    }

    [Fact]
    public void Throw_on_non_scoring_sector_continues_without_changes()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];

        var dart = new ThrowData(5, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);

        Assert.Equal(0, updated.Score);
        Assert.Equal(0, updated.HitsOn20);
        Assert.Equal(0, updated.HitsOn19);
        Assert.Equal(0, updated.HitsOn18);
        Assert.Equal(0, updated.HitsOn17);
        Assert.Equal(0, updated.HitsOn16);
        Assert.Equal(0, updated.HitsOn15);
        Assert.Equal(0, updated.HitsOnBull);

        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }
    
    [Fact]
    public void Single_hit_increments_sector_count()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);
        
        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);

        Assert.Equal(1, updated.HitsOn20);

        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }
    
    [Fact]
    public void Triple_hit_counts_as_three_when_multipliers_enabled()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);

        Assert.Equal(3, updated.HitsOn20);
    }
    
    [Fact]
    public void Triple_hit_counts_as_one_when_multipliers_disabled()
    {
        var (_, mode, players, allScores)
            = Setup(countMultipliers: false);
        var player1 = players[0];

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var updated = Assert.IsType<CricketScore>(result.UpdatedScore);

        Assert.Equal(1, updated.HitsOn20);
    }
    
    [Fact]
    public void Score_increase_if_not_closed()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];
        allScores[player1.Id] = p1Score with { HitsOn20 = 2 }; // Opened (current)
        allScores[player2.Id] = p2Score with { HitsOn20 = 2 }; // Opened

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var p1Updated = Assert.IsType<CricketScore>(result.UpdatedScore);

        // P1
        Assert.Equal(3, p1Updated.HitsOn20);
        Assert.Equal(40, p1Updated.Score);
    }
    
    [Fact]
    public void No_score_increase_if_closed()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];
        allScores[player1.Id] = p1Score with { HitsOn20 = 2 }; // Opened (current)
        allScores[player2.Id] = p2Score with { HitsOn20 = 3 }; // Closed

        var dart = new ThrowData(20, 3);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        var p1Updated = Assert.IsType<CricketScore>(result.UpdatedScore);

        // P1
        Assert.Equal(3, p1Updated.HitsOn20);
        Assert.Equal(0, p1Updated.Score);
    }

    [Fact]
    public void Win_when_all_sectors_closed_and_same_score()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];

        // P1 will close all sectors in the next throw
        allScores[player1.Id] = p1Score with 
        {
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 3,
            HitsOn19 = 3,
            HitsOn20 = 2,
            HitsOnBull = 3,
            Score = 100
        };

        // P2 didn't close all sectors yet
        allScores[player2.Id] = p2Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 2,
            HitsOn19 = 0,
            HitsOn20 = 2,
            HitsOnBull = 1,
            Score = 100
        };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Win_when_all_sectors_closed_and_higher_score()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];

        // P1 will close all sectors in the next throw
        allScores[player1.Id] = p1Score with 
        {
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 3,
            HitsOn19 = 3,
            HitsOn20 = 2,
            HitsOnBull = 3,
            Score = 120
        };

        // P2 didn't close all sectors yet
        allScores[player2.Id] = p2Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 2,
            HitsOn19 = 0,
            HitsOn20 = 2,
            HitsOnBull = 1,
            Score = 100
        };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Win, result.Outcome);
    }

    [Fact]
    public void Continue_when_all_sectors_closed_but_lower_score()
    {
        var (_, mode, players, allScores)
            = Setup();
        var player1 = players[0];
        var player2 = players[1];
        var p1Score = (CricketScore)allScores[player1.Id];
        var p2Score = (CricketScore)allScores[player2.Id];

        // P1 will close all sectors in the next throw
        allScores[player1.Id] = p1Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 3,
            HitsOn19 = 3,
            HitsOn20 = 2,
            HitsOnBull = 3,
            Score = 100
        };

        // P2 didn't close all sectors yet
        allScores[player2.Id] = p2Score with 
        { 
            HitsOn15 = 3,
            HitsOn16 = 3,
            HitsOn17 = 3,
            HitsOn18 = 2,
            HitsOn19 = 0,
            HitsOn20 = 2,
            HitsOnBull = 1,
            Score = 200
        };

        var dart = new ThrowData(20, 1);
        var result = mode.EvaluateThrow(player1.Id, dart, allScores);

        Assert.Equal(ThrowOutcome.Continue, result.Outcome);
    }

    [Fact]
    public void Workflow()
    {
        var (game, _, players, _)
            = Setup();
        var p1 = players[0];
        var p2 = players[1];

        game.RegisterThrow(p1.Id, new ThrowData(20, 1));
        game.RegisterThrow(p1.Id, new ThrowData(20, 1));
        game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // 20 closed, 40 score
        AssertCannotThrow(game, p1.Id);

        game.RegisterThrow(p2.Id, new ThrowData(19, 1));
        game.RegisterThrow(p2.Id, new ThrowData(19, 1));
        game.RegisterThrow(p2.Id, new ThrowData(19, 3)); // 19 closed, 38 score
        AssertCannotThrow(game, p2.Id);

        game.RegisterThrow(p1.Id, new ThrowData(19, 3)); // 19 closed
        game.RegisterThrow(p1.Id, new ThrowData(18, 3)); // 18 closed
        game.RegisterThrow(p1.Id, new ThrowData(17, 3)); // 17 closed

        game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // 20 closed
        game.RegisterThrow(p2.Id, new ThrowData(18, 3)); // 18 closed
        game.RegisterThrow(p2.Id, new ThrowData(17, 3)); // 17 closed

        game.RegisterThrow(p1.Id, new ThrowData(20, 3)); // No score
        game.RegisterThrow(p1.Id, new ThrowData(25, 2)); // 25 closed
        game.RegisterThrow(p1.Id, new ThrowData(25, 1)); // 65 score

        game.RegisterThrow(p2.Id, new ThrowData(20, 3)); // No score
        game.RegisterThrow(p2.Id, new ThrowData(25, 2)); // No score
        game.RegisterThrow(p2.Id, new ThrowData(16, 3)); // 16 closed

        var p1Score = Assert.IsType<CricketScore>(game.ScoreStates[p1.Id]);
        var p2Score = Assert.IsType<CricketScore>(game.ScoreStates[p2.Id]);

        // P1
        Assert.Equal(40, p1Score.Score);
        Assert.Equal(3, p1Score.HitsOn20);
        Assert.Equal(3, p1Score.HitsOn19);
        Assert.Equal(3, p1Score.HitsOn18);
        Assert.Equal(3, p1Score.HitsOn17);
        Assert.Equal(0, p1Score.HitsOn16);
        Assert.Equal(0, p1Score.HitsOn15);

        // P2
        Assert.Equal(38, p2Score.Score);
        Assert.Equal(3, p2Score.HitsOn20);
        Assert.Equal(3, p2Score.HitsOn19);
        Assert.Equal(3, p2Score.HitsOn18);
        Assert.Equal(3, p2Score.HitsOn17);
        Assert.Equal(3, p2Score.HitsOn16);
        Assert.Equal(0, p1Score.HitsOn15);
    }
}