using Microsoft.Data.Sqlite;
using ChessGameService.App;
using ChessGameServiceImplementation = ChessGameService.App.ChessGameService;
using Xunit;

namespace ChessGameService.Tests;

public sealed class ChessGamePersistenceTests
{
    [Fact]
    public async Task GameAndMoveHistorySurviveServiceRecreation()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"mcp-chess-reload-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "games.db")
        }.ToString();

        try
        {
            var firstService = new ChessGameServiceImplementation(new SqliteGameRepository(connectionString));
            var game = await firstService.CreateGameAsync();
            await firstService.MakeMoveAsync(game.Id, "e4");
            var afterSecondMove = await firstService.MakeMoveAsync(game.Id, "e5");

            var restartedService = new ChessGameServiceImplementation(new SqliteGameRepository(connectionString));
            var restored = await restartedService.GetBoardAsync(game.Id);

            Assert.Equal(afterSecondMove.Fen, restored.Fen);
            Assert.Equal("White", restored.SideToMove);
            Assert.Contains("e4", restored.Pgn);
            Assert.Contains("e5", restored.Pgn);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }
}