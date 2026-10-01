$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$contextPath = Join-Path $repoRoot "PROJECT_CONTEXT.md"

if (-not (Test-Path -LiteralPath $contextPath)) {
    $payload = @{ systemMessage = "ChessGameService project context file is missing. Read the repository README and inspect the code before making changes." }
}
else {
    $context = Get-Content -LiteralPath $contextPath -Raw
    $payload = @{ systemMessage = "Repository project context restored from PROJECT_CONTEXT.md. Treat it as durable project notes, verify current state before relying on old validation results.`n`n$context" }
}

ConvertTo-Json -InputObject $payload -Compress
