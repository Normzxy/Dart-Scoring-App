using Domain.Interfaces;
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
    private PlayerScore? _turnSnapshot;
    private bool IsGameFinished { get; set; }
    private int _dartsThrown;
    private int _currentPlayerIdx;
    private int _legStartingPlayerIdx;
    private int _setStartingPlayerIdx;

    public Guid Id { get; } = Guid.NewGuid();
    public IReadOnlyList<Player> Players => _players.AsReadOnly();
    public IReadOnlyDictionary<Guid, PlayerScore> ScoreStates => _scoreStates;
    public IReadOnlyList<Throw> History => _history.AsReadOnly();
    public Guid? WinnerId { get; private set; }

    public Game(IGameMode gameMode, List<Player> players)
    {
        _gameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));
        ArgumentNullException.ThrowIfNull(players);
        _players.AddRange(players);
        _gameMode.ValidatePlayers(_players);

        foreach (var player in _players)
            _scoreStates[player.Id] = _gameMode.CreateInitialScore(player.Id);

        _dartsThrown = 0;
        _currentPlayerIdx = 0;
        _legStartingPlayerIdx = 0;
        _setStartingPlayerIdx = 0;
    }

    private Player CurrentPlayer => _players[_currentPlayerIdx];

    public PlayerScore GetPlayerState(Guid playerId) =>
        _scoreStates.TryGetValue(playerId, out var state) ? state : throw new KeyNotFoundException();

    public IReadOnlyDictionary<Guid, PlayerScore> GetAllPlayerStates()
        => _scoreStates;

    public ThrowEvaluationResult RegisterThrow(Guid playerId, ThrowData throwData)
    {
        if (IsGameFinished)
            throw new InvalidOperationException("Game already finished.");

        if (playerId != CurrentPlayer.Id)
            throw new InvalidOperationException("Not this player's turn.");

        ArgumentNullException.ThrowIfNull(throwData);

        // Scores snapshot before editing.
        if (_dartsThrown == 0)
            _turnSnapshot= _scoreStates[playerId];

        // Save throw data with aditional idenitifiers.
        var @throw = new Throw(playerId, throwData);
        _history.Add(@throw);

        // Score evaluation for a specific game mode, based on a throw info.
        var throwEvaluation = _gameMode.EvaluateThrow(
            playerId,
            throwData,
            _scoreStates);

        // Update other player's score if needed.
        if (throwEvaluation.OtherUpdatedScores != null)
            foreach (var kv in throwEvaluation.OtherUpdatedScores)
                _scoreStates[kv.Key] = kv.Value;

        // Game state update for latest throw.
        switch (throwEvaluation.Outcome)
        {
            case ThrowOutcome.Bust:
                _scoreStates[playerId] = _turnSnapshot ?? throw new InvalidOperationException("Turn snapshot missing during bust.");
                EndTurn();
                return throwEvaluation;

            case ThrowOutcome.Win:
                _scoreStates[playerId] = throwEvaluation.UpdatedScore!;
                IsGameFinished = true;
                WinnerId = playerId;
                return throwEvaluation;

            case ThrowOutcome.Continue:
                _scoreStates[playerId] = throwEvaluation.UpdatedScore!;
                _dartsThrown++;

                switch (throwEvaluation.Proggress)
                {
                    case ProggressInfo.LegWon:
                        EndLeg();
                        return throwEvaluation;

                    case ProggressInfo.SetWon:
                        EndSet();
                        return throwEvaluation;

                    case ProggressInfo.None:
                        break;

                    default:
                        throw new InvalidOperationException("Unsupported ProgressInfo.");
                }

                if (_dartsThrown >= _gameMode.DartsPerTurn)
                    EndTurn();

                return throwEvaluation;

            default:
                throw new InvalidOperationException("Unsupported ThrowOutcome.");
        }
    }

    private void EndTurn()
    {
        _currentPlayerIdx = (_currentPlayerIdx + 1) % _players.Count;
        _dartsThrown = 0;
        _turnSnapshot = null;
    }

    private void EndLeg()
    {
        _legStartingPlayerIdx = (_legStartingPlayerIdx + 1) % _players.Count;
        _currentPlayerIdx = _legStartingPlayerIdx;
        _dartsThrown = 0;
        _turnSnapshot = null;
    }

    private void EndSet()
    {
        _setStartingPlayerIdx = (_setStartingPlayerIdx + 1) % _players.Count;
        _legStartingPlayerIdx = _setStartingPlayerIdx;
        _currentPlayerIdx = _legStartingPlayerIdx;
        _dartsThrown = 0;
        _turnSnapshot = null;
    }
}