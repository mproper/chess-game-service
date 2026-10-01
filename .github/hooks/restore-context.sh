#!/usr/bin/env bash
set -eu

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
context_file="$repo_root/PROJECT_CONTEXT.md"

if [ -f "$context_file" ]; then
    context="$(cat "$context_file")"
    context="${context//\\/\\\\}"
    context="${context//\"/\\\"}"
    context="${context//$'\r'/}"
    context="${context//$'\n'/\\n}"
    message="Repository project context restored from PROJECT_CONTEXT.md. Treat it as durable project notes, verify current state before relying on old validation results.\\n\\n$context"
else
    message="ChessGameService project context file is missing. Read the repository README and inspect the code before making changes."
fi

printf '{"systemMessage":"%s"}\n' "$message"
