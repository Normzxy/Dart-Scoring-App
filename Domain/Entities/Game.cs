using Domain.Modes;
using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>
/// Aggregate root representing the overall state of a game.
/// Stores the current game state and the history of the match, without any mode-specific rules.
/// </summary>
public class Game
{
    private readonly IGameMode _gameMode;
    private readonly List<Player> _players = new();
    private readonly Dictionary<Guid, PlayerScore> _scoreStates = new();
    private readonly List<Throw> _history = new();

    private PlayerScore? _turnSnapshot = null;
    private int _currentPlayerIdx = 0;
    private int _dartsThrown = 0;

    public Guid Id { get; } = Guid.NewGuid();
    public IReadOnlyList<Player> Players => _players.AsReadOnly();
    public IReadOnlyList<Throw> History => _history.AsReadOnly();
    public bool IsGameFinished { get; private set; } = false;
    public Guid? WinnerId { get; private set; } = null;

    public Game(IGameMode gameMode, List<Player> players)
    {
        _gameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));

        ArgumentNullException.ThrowIfNull(players);
        _players.AddRange(players);

        if (_players.Count == 0)
        {
            throw new ArgumentException("At least one player is required.", nameof(players));
        }

        foreach (var player in _players)
        {
            _scoreStates[player.Id] = _gameMode.CreateInitialScore(player.Id);
        }
    }
    
    public Player CurrentPlayer => _players[_currentPlayerIdx];
    
    public PlayerScore GetPlayerState(Guid playerId) =>
        _scoreStates.TryGetValue(playerId, out var state) ? state : throw new KeyNotFoundException();

    public IReadOnlyDictionary<Guid, PlayerScore> GetAllPlayerStates() =>
        _scoreStates;
    
    public ThrowEvaluationResult RegisterThrow(Guid playerId, ThrowData throwData)
    {
        if (IsGameFinished) throw new InvalidOperationException("Game already finished.");
        if (playerId != CurrentPlayer.Id)
        {
            throw new InvalidOperationException("Not this player's turn.");
        }
        ArgumentNullException.ThrowIfNull(throwData);
        
        // Scores snapshot before editing.
        if (_dartsThrown == 0)
        {
            _turnSnapshot = _scoreStates[playerId];
        }
        
        var currentScore = _scoreStates[playerId];
        
        // Save throw data with aditional idenitifiers.
        var @throw = new Throw(playerId, throwData);
        _history.Add(@throw);
        
        // Score evaluation for a specific game mode, based on a throw info.
        var throwEvaluation = _gameMode.EvaluateThrow(playerId, currentScore, throwData);

        // Update other player's score if needed.
        if (throwEvaluation.OtherUpdatedStates != null)
        {
            foreach (var kv in throwEvaluation.OtherUpdatedStates)
            {
                _scoreStates[kv.Key] = kv.Value;
            }
        }

        // Game state update for latest throw.
        switch (throwEvaluation.Outcome)
        {
            case ThrowOutcome.Bust:
                _scoreStates[playerId] = _turnSnapshot ?? throw new InvalidOperationException("Turn snapshot missing during bust.");
                EndTurn();
                return throwEvaluation;

            case ThrowOutcome.Win:
                _scoreStates[playerId] = throwEvaluation.UpdatedScore;
                IsGameFinished = true;
                WinnerId = playerId;
                return throwEvaluation;

            case ThrowOutcome.Continue:
                _scoreStates[playerId] = throwEvaluation.UpdatedScore;
                _dartsThrown++;
                if (_dartsThrown >= 3)
                    EndTurn();
                return throwEvaluation;

            default:
                throw new InvalidOperationException("Unsupported ThrowOutcome.");
        }
    }
    
    private void EndTurn()
    {
        _dartsThrown = 0;
        _turnSnapshot = null;
        _currentPlayerIdx = (_currentPlayerIdx + 1) % _players.Count;
    }
}