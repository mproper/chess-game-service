using Chess;
using ChessGameService.App;
using ChessGameServiceImplementation = ChessGameService.App.ChessGameService;
using Xunit;

namespace ChessGameService.Tests;

public sealed class ChessGameServiceTests
{
    [Fact]
    public async Task CreateGameStartsAtStandardPositionAndListsSanMoves()
    {
        var repository = new InMemoryGameRepository();
        var service = new ChessGameServiceImplementation(repository);

        var game = await service.CreateGameAsync();
        var legalMoves = await service.GetLegalMovesAsync(game.Id);

        Assert.Equal(new ChessBoard().ToFen(), game.Fen);
        Assert.Equal("White", game.SideToMove);
        Assert.Equal("InProgress", game.Status);
        Assert.Null(game.Result);
        Assert.Contains("e4", legalMoves);
        Assert.NotEmpty(game.Board);
        Assert.Empty(repository.Games[game.Id].MovesSan);
    }

    [Fact]
    public async Task MakeMovePersistsLegalMoveAndRejectsIllegalMoveWithoutSaving()
    {
        var repository = new InMemoryGameRepository();
        var service = new ChessGameServiceImplementation(repository);
        var game = await service.CreateGameAsync();

        var moved = await service.MakeMoveAsync(game.Id, "e4");
        var savesAfterSuccess = repository.SaveCount;
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.MakeMoveAsync(game.Id, "e4"));

        Assert.Equal("Black", moved.SideToMove);
        Assert.Contains("Illegal or invalid SAN", exception.Message);
        Assert.Equal(savesAfterSuccess, repository.SaveCount);
        Assert.Equal(new[] { "e4" }, repository.Games[game.Id].MovesSan);
    }

    [Fact]
    public async Task CheckmateReportsCheckAndWinningResult()
    {
        var service = new ChessGameServiceImplementation(new InMemoryGameRepository());
        var game = await service.CreateGameAsync();
        foreach (var move in new[] { "e4", "e5", "Qh5", "Nc6", "Bc4", "Nf6", "Qxf7#" })
        {
            game = await service.MakeMoveAsync(game.Id, move);
        }

        Assert.True(game.InCheck);
        Assert.NotEqual("InProgress", game.Status);
        Assert.Equal("1-0", game.Result);
        Assert.Contains("Qxf7#", game.Pgn);
    }

    [Fact]
    public async Task SupportsCastlingEnPassantAndPromotion()
    {
        var service = new ChessGameServiceImplementation(new InMemoryGameRepository());
        var castleGame = await service.CreateGameAsync();
        foreach (var move in new[] { "e4", "e5", "Nf3", "Nc6", "Bc4", "Nf6" })
        {
            castleGame = await service.MakeMoveAsync(castleGame.Id, move);
        }

        Assert.Contains("O-O", await service.GetLegalMovesAsync(castleGame.Id));
        castleGame = await service.MakeMoveAsync(castleGame.Id, "O-O");
        Assert.Contains("K", castleGame.Board);

        var enPassantGame = await service.CreateGameAsync();
        foreach (var move in new[] { "e4", "a6", "e5", "d5" })
        {
            enPassantGame = await service.MakeMoveAsync(enPassantGame.Id, move);
        }

        Assert.Contains("exd6", await service.GetLegalMovesAsync(enPassantGame.Id));
        enPassantGame = await service.MakeMoveAsync(enPassantGame.Id, "exd6");
        Assert.Contains("exd6", enPassantGame.Pgn);

        var promotionGame = await service.CreateGameAsync();
        foreach (var move in new[] { "a4", "h5", "a5", "h4", "a6", "h3", "axb7", "hxg2" })
        {
            promotionGame = await service.MakeMoveAsync(promotionGame.Id, move);
        }

        Assert.Contains("bxa8=Q", await service.GetLegalMovesAsync(promotionGame.Id));
        promotionGame = await service.MakeMoveAsync(promotionGame.Id, "bxa8=Q");
        Assert.Contains("bxa8=Q", promotionGame.Pgn);
    }

    [Fact]
    public async Task ReplayingStoredHistoryPreservesRepetitionDraw()
    {
        var repository = new InMemoryGameRepository();
        var service = new ChessGameServiceImplementation(repository);
        var game = await service.CreateGameAsync();
        foreach (var move in new[] { "Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1", "Ng8" })
        {
            game = await service.MakeMoveAsync(game.Id, move);
        }

        var reloaded = await new ChessGameServiceImplementation(repository).GetBoardAsync(game.Id);

        Assert.Equal("1/2-1/2", reloaded.Result);
        Assert.NotEqual("InProgress", reloaded.Status);
        Assert.Equal(repository.Games[game.Id].Fen, reloaded.Fen);
    }

    [Fact]
    public async Task ResetRestoresStandardPositionAndKeepsGameIdentity()
    {
        var repository = new InMemoryGameRepository();
        var service = new ChessGameServiceImplementation(repository);
        var game = await service.CreateGameAsync();
        await service.MakeMoveAsync(game.Id, "e4");

        var reset = await service.ResetGameAsync(game.Id);

        Assert.Equal(game.Id, reset.Id);
        Assert.Equal(new ChessBoard().ToFen(), reset.Fen);
        Assert.Equal("White", reset.SideToMove);
        Assert.Equal("InProgress", reset.Status);
        Assert.Empty(repository.Games[game.Id].MovesSan);
    }

    [Fact]
    public async Task GamesHaveIndependentPositions()
    {
        var service = new ChessGameServiceImplementation(new InMemoryGameRepository());
        var first = await service.CreateGameAsync();
        var second = await service.CreateGameAsync();

        await service.MakeMoveAsync(first.Id, "e4");

        var firstBoard = await service.GetBoardAsync(first.Id);
        var secondBoard = await service.GetBoardAsync(second.Id);
        Assert.Equal("Black", firstBoard.SideToMove);
        Assert.Equal("White", secondBoard.SideToMove);
        Assert.NotEqual(firstBoard.Fen, secondBoard.Fen);
    }

    [Fact]
    public async Task MissingGameHasUsefulErrorAndConcurrentMovesAreSerialized()
    {
        var repository = new InMemoryGameRepository();
        var service = new ChessGameServiceImplementation(repository);
        var missing = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetBoardAsync("absent"));
        Assert.Contains("absent", missing.Message);

        var game = await service.CreateGameAsync();
        var attempts = await Task.WhenAll(
            AttemptMoveAsync(service, game.Id, "e4"),
            AttemptMoveAsync(service, game.Id, "d4"));

        Assert.Equal(1, attempts.Count(succeeded => succeeded));
        Assert.Single(repository.Games[game.Id].MovesSan);
        Assert.Equal(1, repository.SaveCount);
    }

    private static async Task<bool> AttemptMoveAsync(ChessGameServiceImplementation service, string id, string san)
    {
        try
        {
            await service.MakeMoveAsync(id, san);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed class InMemoryGameRepository : IGameRepository
    {
        private readonly Dictionary<string, StoredGame> _games = new();
        private readonly object _sync = new();

        public IReadOnlyDictionary<string, StoredGame> Games
        {
            get
            {
                lock (_sync)
                {
                    return new Dictionary<string, StoredGame>(_games);
                }
            }
        }

        public int SaveCount { get; private set; }

        public Task CreateAsync(StoredGame game, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _games.Add(game.Id, game);
            }
            return Task.CompletedTask;
        }

        public Task<StoredGame?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _games.TryGetValue(id, out var game);
                return Task.FromResult(game);
            }
        }

        public Task<IReadOnlyList<StoredGame>> ListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                return Task.FromResult<IReadOnlyList<StoredGame>>(_games.Values.ToArray());
            }
        }

        public Task SaveAsync(StoredGame game, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _games[game.Id] = game;
                SaveCount++;
            }
            return Task.CompletedTask;
        }
    }
}