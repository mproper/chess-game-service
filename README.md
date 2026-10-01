<div align="center">

# ♟ Chess Game Service

**A persistent, multi-game chessboard exposed as an MCP server.**

![.NET 9](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![MCP](https://img.shields.io/badge/protocol-MCP-20232A)
![Status](https://img.shields.io/badge/status-experimental-orange)
![GitHub Copilot](https://img.shields.io/badge/experiment-GitHub%20Copilot-8957E5)

*A test implementation of a chessboard MCP server, built with GitHub Copilot.*

</div>

---

## What it does

Run multiple independent chess games through MCP tools. Legal moves and game outcomes are provided by [Gera.Chess](https://www.nuget.org/packages/Gera.Chess); game positions and ordered SAN histories are stored locally in SQLite and survive server restarts.

| MCP tool | Purpose |
| --- | --- |
| `create_game` | Create a game in the standard starting position. |
| `list_games` | List saved games, status, and side to move. |
| `get_board` | Inspect a game's board, FEN, turn, check, and result. |
| `get_legal_moves` | List legal moves in standard algebraic notation (SAN). |
| `make_move` | Validate and persist a legal SAN move. |
| `reset_game` | Reset a game and clear its move history. |

Invalid moves do not change the saved game. Each game has its own stable ID.

## Run locally

Requires the .NET 9 SDK.

```powershell
dotnet build .\ChessGameService.csproj
dotnet test .\tests\ChessGameService.Tests.csproj
dotnet run --project .\ChessGameService.csproj
```

The server uses MCP's standard input/output transport. Add it to an MCP client as a local stdio server, using `dotnet run --project <path-to-checkout>/ChessGameService.csproj` as the command and arguments.

## Data

The default database is `%LOCALAPPDATA%\McpChess\games.db` on Windows. Override the path with the `Chess__DatabasePath` environment variable or the `Chess:DatabasePath` configuration key.

## Project note

This repository is an experimental/test implementation created with GitHub Copilot. It is intended for evaluation and learning, not as a production chess platform. Review the rules library and its supported draw adjudication before relying on it for tournament or rated play.

## Copilot continuity

`PROJECT_CONTEXT.md` stores durable project decisions and current technical context. `.github/copilot-instructions.md` asks Copilot to load it automatically, and a VS Code `SessionStart` hook injects its contents when hooks are enabled and the workspace is trusted. The hook has Windows and POSIX scripts. Hook execution depends on the selected agent harness; other editors or remote/cloud harnesses may not run VS Code workspace hooks. This restores repository knowledge, not private chat history or hidden model memory.
