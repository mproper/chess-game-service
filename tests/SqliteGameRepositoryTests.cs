using Microsoft.Data.Sqlite;
using ChessGameService.App;
using Xunit;

namespace ChessGameService.Tests;

public sealed class SqliteGameRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mcp-chess-{Guid.NewGuid():N}");
    private readonly string _databasePath;

    public SqliteGameRepositoryTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "games.db");
    }

    [Fact]
    public async Task CreateGetListSaveAndReload_PreservesHistoryAndIsolatesIds()
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString();
        var repository = new SqliteGameRepository(connectionString);
        var first = new StoredGame(
            "game-1", "fen-one", ["e4", "e5", "Nf3"], "active", null,
            new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 10, 1, 12, 5, 0, TimeSpan.FromHours(2)));
        var second = new StoredGame(
            "game-2", "fen-two", ["d4"], "finished", "1-0",
            new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 1, 10, 1, 0, TimeSpan.Zero));

        await repository.CreateAsync(first);
        await repository.CreateAsync(second);

        AssertGameEqual(first, await repository.GetAsync(first.Id));
        var listedGames = await repository.ListAsync();
        Assert.Equal(new[] { first.Id, second.Id }, listedGames.Select(game => game.Id));
        AssertGameEqual(first, listedGames[0]);
        AssertGameEqual(second, listedGames[1]);

        var updated = first with
        {
            Fen = "updated-fen",
            MovesSan = ["e4", "e5", "Nf3", "Nc6"],
            UpdatedAt = first.UpdatedAt.AddMinutes(2)
        };
        await repository.SaveAsync(updated);

        var reloadedRepository = new SqliteGameRepository(connectionString);
        AssertGameEqual(updated, await reloadedRepository.GetAsync(first.Id));
        AssertGameEqual(second, await reloadedRepository.GetAsync(second.Id));
        var reloadedGames = await reloadedRepository.ListAsync();
        Assert.Equal(new[] { updated.Id, second.Id }, reloadedGames.Select(game => game.Id));
        AssertGameEqual(updated, reloadedGames[0]);
        AssertGameEqual(second, reloadedGames[1]);

        var upserted = second with { Status = "archived" };
        await reloadedRepository.SaveAsync(upserted);
        AssertGameEqual(upserted, await new SqliteGameRepository(connectionString).GetAsync(second.Id));
    }

    private static void AssertGameEqual(StoredGame expected, StoredGame? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Fen, actual.Fen);
        Assert.Equal(expected.MovesSan, actual.MovesSan);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        Assert.Equal(expected.UpdatedAt, actual.UpdatedAt);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}