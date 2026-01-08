using Domain.Modes;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Game
{
    private readonly IGameMode _gameMode;
    private readonly List<Player> _players = new();
    private readonly Dictionary<Guid, int> _score = new();
    private readonly List<Throw> _history = new();
    private readonly Dictionary<Guid, int> _turnStartScoreSnapshot = new();
    
    private int _currentPlayerIndex = 0;
    private int _dartsThrown = 0;

    public Guid Id { get; } = Guid.NewGuid();
    public IReadOnlyList<Player> Players => _players.AsReadOnly();
    public IReadOnlyList<Throw> History => _history.AsReadOnly();
    public bool IsFinished { get; private set; } = false;
    public Guid? WinnerId { get; private set; } = null;

    /// <summary>
    /// Creates new game based on specified mode.
    /// </summary>
    public Game(IEnumerable<Player> players, IGameMode mode)
    {
        _gameMode = mode ?? throw new ArgumentNullException(nameof(mode));
        ArgumentNullException.ThrowIfNull(players);

        _players.AddRange(players);
        if (_players.Count == 0) throw new ArgumentException("At least one player is required.");

        var startingScore = _gameMode.GetStartingScore();
        foreach (var p in _players)
            _score[p.Id] = startingScore;
    }

    public Player CurrentPlayer => _players[_currentPlayerIndex];

    public int ScoreForPlayer(Guid playerId) => _score.TryGetValue(playerId, out var v) ? v : throw new KeyNotFoundException();

    // _currentPlayerIndex instead of playerId?
    public ThrowEvaluationResult RegisterThrow(Guid playerId, ThrowData throwData)
    {
        if (IsFinished) throw new InvalidOperationException("Game already finished.");
        
        if (playerId != CurrentPlayer.Id) throw new InvalidOperationException("Something wrong with player's ID.");
        
        ArgumentNullException.ThrowIfNull(throwData);

        if (_dartsThrown == 0)
        {
            _turnStartScoreSnapshot[playerId] = _score[playerId];
        }
        
        // Throw result is evaluated here, based on a specific game mode.
        var modeBasedEvaluation = _gameMode.EvaluateThrow(
            playerId, _score[playerId], throwData);
        
        var newThrow = new Throw(playerId, throwData);
        _history.Add(newThrow);

        _score[playerId] = modeBasedEvaluation.UpdatedScoreValue;

        switch (modeBasedEvaluation.Outcome)
        {
            case ThrowOutcome.Bust:
                _score[playerId] = _turnStartScoreSnapshot[playerId];
                EndTurn();
                return modeBasedEvaluation;

            case ThrowOutcome.Win:
                IsFinished = true;
                WinnerId = modeBasedEvaluation.WinnerId;
                return modeBasedEvaluation;

            case ThrowOutcome.Continue:
                _dartsThrown++;
                if (_dartsThrown >= 3)
                    EndTurn();
                return modeBasedEvaluation;

            default:
                throw new InvalidOperationException("Something wrong with evaluating the throw outcome.");
        }
    }
    
    private void EndTurn()
    {
        _dartsThrown = 0;
        _turnStartScoreSnapshot.Remove(CurrentPlayer.Id);
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
    }
}