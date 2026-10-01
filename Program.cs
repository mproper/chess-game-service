using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using ChessGameService.App;

var builder = Host.CreateApplicationBuilder(args);

var databasePath = builder.Configuration["Chess:DatabasePath"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpChess", "games.db");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

builder.Services.AddSingleton<IGameRepository>(_ => new SqliteGameRepository(connectionString));
builder.Services.AddSingleton<IChessGameService, global::ChessGameService.App.ChessGameService>();
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ChessTools>();

builder.Build().Run();

public sealed class ChessTools(IChessGameService gameService)
{
    [McpServerTool(Name = "create_game"), Description("Create and persist a new chess game in the standard starting position.")]
    public Task<GameView> CreateGame(CancellationToken cancellationToken = default) =>
        gameService.CreateGameAsync(cancellationToken);

    [McpServerTool(Name = "list_games"), Description("List persisted chess games with current status and side to move.")]
    public Task<IReadOnlyList<GameSummary>> ListGames(CancellationToken cancellationToken = default) =>
        gameService.ListGamesAsync(cancellationToken);

    [McpServerTool(Name = "get_board"), Description("Return the board, FEN, turn, check state, and result for a game.")]
    public Task<GameView> GetBoard([Description("The ID returned by create_game.")] string gameId, CancellationToken cancellationToken = default) =>
        gameService.GetBoardAsync(gameId, cancellationToken);

    [McpServerTool(Name = "get_legal_moves"), Description("List all legal moves for the side to move, in standard algebraic notation (SAN).")]
    public Task<IReadOnlyList<string>> GetLegalMoves([Description("The game ID.")] string gameId, CancellationToken cancellationToken = default) =>
        gameService.GetLegalMovesAsync(gameId, cancellationToken);

    [McpServerTool(Name = "make_move"), Description("Apply and persist a legal SAN move in the specified game.")]
    public Task<GameView> MakeMove(
        [Description("The game ID.")] string gameId,
        [Description("A legal move in standard algebraic notation (SAN), such as e4 or Nf3.")] string moveSan,
        CancellationToken cancellationToken = default) =>
        gameService.MakeMoveAsync(gameId, moveSan, cancellationToken);

    [McpServerTool(Name = "reset_game"), Description("Reset the specified game to the standard starting position and clear its move history.")]
    public Task<GameView> ResetGame([Description("The game ID.")] string gameId, CancellationToken cancellationToken = default) =>
        gameService.ResetGameAsync(gameId, cancellationToken);
}
