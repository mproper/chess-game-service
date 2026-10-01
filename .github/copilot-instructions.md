# Project Instructions

- At the start of project work, read `PROJECT_CONTEXT.md`, then check `git status --short --branch` and inspect only the directly relevant code/tests.
- This is the experimental .NET 9 `ChessGameService` MCP server. Follow its committed scope and architecture in `PROJECT_CONTEXT.md`; update that context when durable design or status changes.
- Use the chess library as the legality authority; don't hand-roll chess rules. Persist ordered SAN history and rebuild positions from history, validating against the stored FEN.
- Keep work focused to conserve model usage. Parallelize only independent slices after agreeing shared contracts and file ownership.
- Build with `dotnet build .\ChessGameService.csproj --configuration Release`; test with `dotnet test .\tests\ChessGameService.Tests.csproj --configuration Release`.
- Never discard user changes. Do not commit or push until the user explicitly approves; when asked to review before a commit, stop after preparing changes and show the exact staged/unstaged diff summary.
