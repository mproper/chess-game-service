using System.Collections.Concurrent;
using Chess;

namespace ChessGameService.App;

public sealed class ChessGameService(IGameRepository repository) : IChessGameService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gameLocks = new();

    public async Task<GameView> CreateGameAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var board = CreateBoard();
        var game = ToStoredGame(Guid.NewGuid().ToString("N"), board, [], now, now);
        await repository.CreateAsync(game, cancellationToken);
        return ToGameView(game.Id, board);
    }

    public async Task<IReadOnlyList<GameSummary>> ListGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = await repository.ListAsync(cancellationToken);
        return games.Select(game =>
        {
            var board = Replay(game);
            return ToGameSummary(game, board);
        }).ToArray();
    }

    public async Task<GameView> GetBoardAsync(string id, CancellationToken cancellationToken = default)
    {
        var game = await GetRequiredGameAsync(id, cancellationToken);
        return ToGameView(game.Id, Replay(game));
    }

    public async Task<IReadOnlyList<string>> GetLegalMovesAsync(string id, CancellationToken cancellationToken = default)
    {
        var game = await GetRequiredGameAsync(id, cancellationToken);
        var board = Replay(game);
        return board.Moves(generateSan: true)
            .Select(move => move.San)
            .OfType<string>()
            .Where(san => !string.IsNullOrWhiteSpace(san))
            .ToArray();
    }

    public async Task<GameView> MakeMoveAsync(string id, string moveSan, CancellationToken cancellationToken = default)
    {
        var gameLock = _gameLocks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        await gameLock.WaitAsync(cancellationToken);
        try
        {
            var game = await GetRequiredGameAsync(id, cancellationToken);
            var board = Replay(game);
            if (!TryParseLegalMove(board, moveSan, out var move))
            {
                throw new ArgumentException($"Illegal or invalid SAN move '{moveSan}' for game '{id}'.", nameof(moveSan));
            }

            var canonicalSan = board.ParseToSan(move);
            board.Move(move);
            var updated = ToStoredGame(
                game.Id,
                board,
                [.. game.MovesSan, canonicalSan],
                game.CreatedAt,
                DateTimeOffset.UtcNow);
            await repository.SaveAsync(updated, cancellationToken);
            return ToGameView(updated.Id, board);
        }
        finally
        {
            gameLock.Release();
        }
    }

    public async Task<GameView> ResetGameAsync(string id, CancellationToken cancellationToken = default)
    {
        var gameLock = _gameLocks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1));
        await gameLock.WaitAsync(cancellationToken);
        try
        {
            var game = await GetRequiredGameAsync(id, cancellationToken);
            var board = CreateBoard();
            var updated = ToStoredGame(game.Id, board, [], game.CreatedAt, DateTimeOffset.UtcNow);
            await repository.SaveAsync(updated, cancellationToken);
            return ToGameView(updated.Id, board);
        }
        finally
        {
            gameLock.Release();
        }
    }

    private async Task<StoredGame> GetRequiredGameAsync(string id, CancellationToken cancellationToken)
    {
        var game = await repository.GetAsync(id, cancellationToken);
        return game ?? throw new KeyNotFoundException($"Game '{id}' was not found.");
    }

    private static ChessBoard CreateBoard() => new() { AutoEndgameRules = AutoEndgameRules.All };

    private static ChessBoard Replay(StoredGame game)
    {
        var board = CreateBoard();
        foreach (var moveSan in game.MovesSan)
        {
            if (!TryParseLegalMove(board, moveSan, out var move))
            {
                throw new InvalidOperationException($"Game '{game.Id}' contains invalid stored move '{moveSan}'.");
            }

            board.Move(move);
        }

        var actualFen = board.ToFen();
        if (!string.Equals(actualFen, game.Fen, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Game '{game.Id}' history does not match its stored FEN.");
        }

        return board;
    }

    private static bool TryParseLegalMove(ChessBoard board, string? moveSan, out Move move)
    {
        move = null!;
        if (string.IsNullOrWhiteSpace(moveSan))
        {
            return false;
        }

        try
        {
            if (!board.TryParseFromSan(moveSan, out var parsed) || parsed is null || !board.IsValidMove(parsed))
            {
                return false;
            }

            move = parsed;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException || exception.GetType().Namespace == typeof(ChessBoard).Namespace)
        {
            return false;
        }
    }

    private static StoredGame ToStoredGame(
        string id,
        ChessBoard board,
        string[] movesSan,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var (status, result) = GetStatusAndResult(board);
        return new StoredGame(id, board.ToFen(), movesSan, status, result, createdAt, updatedAt);
    }

    private static GameView ToGameView(string id, ChessBoard board)
    {
        var (status, result) = GetStatusAndResult(board);
        return new GameView(
            id,
            board.ToAscii(),
            board.ToFen(),
            board.Turn.ToString(),
            board.Turn == PieceColor.White ? board.WhiteKingChecked : board.BlackKingChecked,
            status,
            result,
            board.ToPgn());
    }

    private static GameSummary ToGameSummary(StoredGame game, ChessBoard board)
    {
        var (status, result) = GetStatusAndResult(board);
        return new GameSummary(game.Id, status, result, board.Turn.ToString(), game.CreatedAt, game.UpdatedAt);
    }

    private static (string Status, string? Result) GetStatusAndResult(ChessBoard board)
    {
        if (!board.IsEndGame || board.EndGame is not { } endGame)
        {
            return ("InProgress", null);
        }

        var result = endGame.WonSide switch
        {
            var side when Equals(side, PieceColor.White) => "1-0",
            var side when Equals(side, PieceColor.Black) => "0-1",
            _ => "1/2-1/2"
        };
        return (endGame.EndgameType.ToString(), result);
    }
}