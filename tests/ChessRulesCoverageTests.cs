using Chess;
using Xunit;

namespace ChessGameService.Tests;

public sealed class ChessRulesCoverageTests
{
    [Fact]
    public void DetectsStalemate()
    {
        var board = CreateBoard("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");

        Assert.True(board.IsEndGame);
        Assert.Equal("Stalemate", board.EndGame?.EndgameType.ToString());
        Assert.Null(board.EndGame?.WonSide);
        Assert.Empty(board.Moves());
    }

    [Fact]
    public void DetectsFiftyMoveRuleAtHundredHalfMoves()
    {
        var board = CreateBoard("7k/8/8/8/8/8/8/R3K3 w - - 99 50");

        board.Move("Ra2");

        Assert.True(board.IsEndGame);
        Assert.Contains("Fifty", board.EndGame?.EndgameType.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static ChessBoard CreateBoard(string fen) => ChessBoard.LoadFromFen(fen, AutoEndgameRules.All);
}