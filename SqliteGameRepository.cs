using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace ChessGameService.App;

public sealed class SqliteGameRepository : IGameRepository
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public SqliteGameRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task CreateAsync(StoredGame game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO games (id, fen, moves_san, status, result, created_at, updated_at)
            VALUES ($id, $fen, $moves, $status, $result, $created, $updated);
            """;
        AddGameParameters(command, game);
        await command.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    public async Task<StoredGame?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, fen, moves_san, status, result, created_at, updated_at
            FROM games WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGame(reader) : null;
    }

    public async Task<IReadOnlyList<StoredGame>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, fen, moves_san, status, result, created_at, updated_at
            FROM games ORDER BY id;
            """;

        var games = new List<StoredGame>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(ReadGame(reader));
        }

        return games;
    }

    public async Task SaveAsync(StoredGame game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        await EnsureInitializedAsync(cancellationToken);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO games (id, fen, moves_san, status, result, created_at, updated_at)
            VALUES ($id, $fen, $moves, $status, $result, $created, $updated)
            ON CONFLICT(id) DO UPDATE SET
                fen = excluded.fen,
                moves_san = excluded.moves_san,
                status = excluded.status,
                result = excluded.result,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at;
            """;
        AddGameParameters(command, game);
        await command.ExecuteNonQueryAsync(cancellationToken);
        transaction.Commit();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS games (
                    id TEXT NOT NULL PRIMARY KEY,
                    fen TEXT NOT NULL,
                    moves_san TEXT NOT NULL,
                    status TEXT NOT NULL,
                    result TEXT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static void AddGameParameters(SqliteCommand command, StoredGame game)
    {
        command.Parameters.AddWithValue("$id", game.Id);
        command.Parameters.AddWithValue("$fen", game.Fen);
        command.Parameters.AddWithValue("$moves", JsonSerializer.Serialize(game.MovesSan));
        command.Parameters.AddWithValue("$status", game.Status);
        command.Parameters.AddWithValue("$result", (object?)game.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", game.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", game.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
    }

    private static StoredGame ReadGame(SqliteDataReader reader)
    {
        return new StoredGame(
            reader.GetString(0),
            reader.GetString(1),
            JsonSerializer.Deserialize<string[]>(reader.GetString(2)) ?? [],
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
}