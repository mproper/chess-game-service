# Project Context

This file is the durable, repository-scoped memory for continuing work on this project across VS Code sessions and machines. It records project facts and decisions, not private account data or the complete transcript of prior chats.

## Identity and status

- Repository: <https://github.com/mproper/chess-game-service>, public, branch `main`.
- Project: `ChessGameService`, a .NET 9 C# MCP server and explicitly an experimental/test implementation built with GitHub Copilot.
- Latest known published commits: `ced1ad5` initial implementation; `275a0f1` publication status.
- The local working tree may have newer uncommitted work; check `git status` and `git log` before editing or claiming the remote is current.

## Agreed scope

- Expose multiple independent standard chess games via MCP; game IDs are stable.
- Rules authority: `Gera.Chess` 1.2.0. `AutoEndgameRules.All` is enabled. The library handles legal SAN moves, check, checkmate, stalemate, castling, en passant, promotion, repetition, and fifty-move draw behavior. Do not implement chess legality in application code.
- Persistence: local SQLite via `Microsoft.Data.Sqlite` 10.0.12.
- MCP transport: standard input/output (stdio).
- There is no chess-playing AI, accounts, remote database, or network transport in scope.

## Architecture

- `Program.cs`: host, DI, SQLite connection configuration, stdio MCP server, and MCP tools.
- `ChessContracts.cs`: `StoredGame`, `GameSummary`, `GameView`, `IGameRepository`, and `IChessGameService` contracts.
- `ChessGameService.cs`: `IChessGameService`; creates boards, replays ordered SAN history, validates stored FEN against the replay, computes views/status, and serializes mutations per game.
- `SqliteGameRepository.cs`: SQLite schema and parameterized transactional create/upsert/get/list operations. Stores the FEN snapshot and ordered SAN move history as JSON.
- MCP tools: `create_game`, `list_games`, `get_board`, `get_legal_moves`, `make_move`, `reset_game`. Moves are standard algebraic notation (SAN).
- Database path configuration: `Chess:DatabasePath` / `Chess__DatabasePath`; default `%LOCALAPPDATA%/McpChess/games.db` on Windows.
- `tests/`: xUnit coverage for rules, invalid move atomicity, isolation between games, persistence, and history replay after service recreation.
- `CHESS_AGENT_PLAN.md`, `TODO.md`, and `PROGRESS.md` document scope and work status; update these when relevant.

## Working conventions

- Start with `git status --short --branch`; never overwrite or discard user changes.
- Keep edits narrowly scoped and avoid re-reading/re-mapping the repository when these context notes answer the question.
- For parallel agent work, settle interfaces first and assign non-overlapping files; don't delegate independent tasks unless requested or clearly useful.
- Preserve game history when changing storage/replay; a FEN snapshot alone is not enough for repetition state.
- A rejected move must not be persisted. Keep same-game mutations serialized.
- Do not commit, push, or publish without explicit user approval. Before committing, show the proposed files/diff and wait for review if requested.

## Verification

From the repository root:

```powershell
dotnet build .\ChessGameService.csproj --configuration Release
dotnet test .\tests\ChessGameService.Tests.csproj --configuration Release
```

Last verified in the prior implementation session: release build passed; 12 tests passed. Re-run after changes rather than treating this as current validation.
