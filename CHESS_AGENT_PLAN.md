# MCP Chess Agent Implementation Plan

## Goal

Replace the hello-world MCP tool with a small C# chess server that lets connected models create and manage multiple chess games. Keep game state in a simple persistent database so games survive server restarts. Chess legality and game outcomes must come from a proven chess rules library, not custom move-validation code.

## Current project

- .NET 9 console application/project named `ChessGameService`, using `Microsoft.Extensions.Hosting` and `ModelContextProtocol` 2.2.0.
- `Program.cs` registers the chess MCP tools over standard input/output transport.
- The server hosts multiple games, each identified by a stable `game_id`.
- SQLite uses `Microsoft.Data.Sqlite` 10.0.12. The database path is configurable with `Chess:DatabasePath` (or `Chess__DatabasePath` as an environment variable); the default is `%LOCALAPPDATA%/McpChess/games.db` on Windows.
- Persistence must retain enough move history to reconstruct rules-engine state after restart, not just a final FEN (for example, repetition claims depend on prior positions).

## Proposed design

1. Use `Gera.Chess` 1.2.0 as the rules engine. Its NuGet documentation describes legal move generation/validation, FEN/SAN/PGN, and optional endgame rules including repetition and the fifty-move rule. Configure `AutoEndgameRules.All`; tests must verify the required behavior against the exact package version. Avoid implementing move legality ourselves.
2. Use `Microsoft.Data.Sqlite` 10.0.12 as the SQLite provider. A game record includes its stable ID, current position/FEN, status, timestamps, and ordered SAN move history. Rehydrate by replaying move history through the rules engine so history-dependent rules remain correct; validate the rebuilt position against the stored FEN.
3. Define a game service that coordinates the rules engine and repository. Create a game in the standard starting position, apply moves only after legality checks, and persist successful changes atomically. Invalid moves must leave both in-memory and database state unchanged.
4. Replace `HelloTool` with MCP tools. Game-specific tools take a required `game_id`:
   - `create_game`: create and persist a standard new game; return its ID and initial state.
   - `list_games`: return IDs and concise status/turn metadata for persisted games.
   - `get_board`: return a game's readable board, FEN, side to move, check status, and game result/status.
   - `get_legal_moves`: return all legal moves for the current side in a documented unambiguous notation (prefer UCI; include SAN if supported cleanly).
   - `make_move`: accept a move in documented notation, apply it if legal, persist it, and return the resulting board/status. Invalid moves return a useful error without changing the game.
   - `reset_game`: reset the identified game to the standard starting position and persist that reset.
5. Register the SQLite repository, game service, and MCP tools with dependency injection, and use the MCP SDK's standard input/output transport. Serialize conflicting mutations per game and use database transactions so concurrent calls cannot corrupt persisted history.
6. Add focused xUnit tests for initial state, ordinary legal/illegal moves, check and checkmate, stalemate, castling, en passant, promotion, repetition, and the fifty-move rule. Also test invalid-move atomicity, independent games, SQLite persistence/reload, move-history replay, and reset behavior.

## Parallel agent work

Use two agents for independent implementation work, then have the coordinating agent integrate and validate. Before parallel edits begin, the coordinator must settle the chess package, database package, persistent data contract, and service/repository interfaces; both agents use those agreed contracts.

- Agent 1, chess domain: implement the rules-engine adapter and game service behavior, including move validation, game status, and reconstruction from ordered move history. Own domain/rules files and focused chess-rule tests.
- Agent 2, persistence: implement the SQLite repository, schema initialization, transaction-safe CRUD/history storage, and repository persistence tests. Own repository/database files and related configuration.
- Coordinator, integration: own MCP tool definitions, dependency injection/host wiring, end-to-end tests, conflict resolution, and final validation. Integrate only after both workstreams honor the shared contracts.

The shared contracts are in `ChessContracts.cs`; an xUnit test project is owned by the coordinator. Agent 1 owns chess domain/service implementation and `ChessGameServiceTests.cs`. Agent 2 owns SQLite repository implementation and `SqliteGameRepositoryTests.cs`. Avoid having either agent edit `Program.cs`, `ChessContracts.cs`, the test project file, or the other agent's files. If a contract needs to change, stop and coordinate it first.

## Rules and state expectations

- The rules engine is the authority for turn order, check, legal moves, castling rights, en passant, promotion, checkmate, stalemate, and supported draw rules (including repetition and move-count rules).
- Moves that leave the moving side's king in check are rejected.
- Every successful move updates the identified game and is durable before the tool reports success.
- Each `game_id` refers to an independent game; operations for one game do not affect another.
- Games and ordered move histories remain available after a server restart.
- A rejected move must not partially change the board or turn.
- Simultaneous calls must not corrupt a game's in-memory or persisted state. Serialize conflicting mutations per game and use transactions for durable updates.
- Do not add network transport, user accounts, a chess-playing AI, or database hosting beyond the local SQLite backend in this task.

## Validation

- Build the application with `dotnet build`.
- Run the focused test project with `dotnet test`.
- Verify MCP tool metadata and input descriptions are clear, and exercise tool workflows: create two games, inspect them, make a legal move in one, confirm the other is unchanged, reject an illegal move without state change, reset, restart the host, then confirm persisted games and move history reload correctly.
- Confirm chess/database package compatibility with the project's target framework and MCP SDK before finalizing dependency choices.
- Run integration tests after both parallel workstreams are merged; report any tests that could not be run.

## Approval checkpoint

This file is the implementation reference and implementation has been approved. If an essential library limitation requires changing the scope, persistence contract, or tool contract, update this plan and request approval before proceeding.
