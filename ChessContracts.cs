namespace ChessGameService.App;

public sealed record StoredGame(
    string Id,
    string Fen,
    string[] MovesSan,
    string Status,
    string? Result,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record GameSummary(
    string Id,
    string Status,
    string? Result,
    string SideToMove,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record GameView(
    string Id,
    string Board,
    string Fen,
    string SideToMove,
    bool InCheck,
    string Status,
    string? Result,
    string Pgn);

public interface IGameRepository
{
    Task CreateAsync(StoredGame game, CancellationToken cancellationToken = default);
    Task<StoredGame?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoredGame>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StoredGame game, CancellationToken cancellationToken = default);
}

public interface IChessGameService
{
    Task<GameView> CreateGameAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameSummary>> ListGamesAsync(CancellationToken cancellationToken = default);
    Task<GameView> GetBoardAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetLegalMovesAsync(string id, CancellationToken cancellationToken = default);
    Task<GameView> MakeMoveAsync(string id, string moveSan, CancellationToken cancellationToken = default);
    Task<GameView> ResetGameAsync(string id, CancellationToken cancellationToken = default);
}
