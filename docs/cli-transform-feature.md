# Feature: HTTP Transform Bridge

## Summary

The extension exposes a localhost HTTP server so external tools (Claude Code hooks, scripts) can trigger XSLT transforms and receive the full output synchronously. This enables an automated feedback loop: Claude edits a stylesheet, the hook fires, the transform runs, the result is returned to Claude for auto-fixing.

## Why

VS Code commands cannot be triggered from shell (`code --execute-command` does not exist). Hooks run as shell subprocesses with no access to the VS Code API. The HTTP server bridges this gap — the hook `curl`s it, the extension runs the transform and returns all output in the response body.

## How It Works

```
Claude edits .xslt/.xsl
  → PostToolUse hook fires
  → curl POST /run-transform { stylesheet, xml }
  → extension spawns dotnet --transform (auto-selects engine)
  → buffers stdout + stderr
  → returns full output in response body
  → Claude reads result, fixes errors, repeats
```

## API

### `POST /run-transform`

**Request body** (JSON):

| Field | Required | Description |
|-------|----------|-------------|
| `stylesheet` | yes | Absolute path to `.xslt`/`.xsl` file |
| `xml` | yes | Absolute path to input XML file |
| `engine` | no | `compiled` or `saxonnet` — auto-detected if omitted |

**Auto-detection:** if `engine` is omitted, the extension reads the stylesheet and selects `compiled` if it contains the `urn:schemas-microsoft-com:xslt` namespace with a `language="C#"` attribute, `saxonnet` otherwise.

**Response codes:**

| Code | Meaning |
|------|---------|
| 200 | Transform succeeded — body is plain text with output + trace logs |
| 400 | Invalid JSON or missing `stylesheet`/`xml` fields |
| 404 | Stylesheet or XML file not found on disk |
| 500 | Transform process failed (non-zero exit code) or spawn error |
| 503 | Adapter DLL not found — run `dotnet build` |

All error responses use `Content-Type: application/json` with body `{ "error": "..." }`.

## Port Discovery

On activation, the server writes its port to `~/.xslt-debugger-port`. On deactivate, the file is deleted. Hooks read this file to discover the port dynamically.

## Security

- Server binds to `127.0.0.1` only — not accessible from network
- Remote address check rejects any request not from `127.0.0.1` / `::1` / `::ffff:127.0.0.1` (403)
- File paths passed as `spawn` argv — no shell injection risk
- POST only — GET returns 404

## Hook Configuration

Active hook in `.claude/settings.json` (this repo):

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "command": "bash -c 'FILE=$(echo \"$CLAUDE_TOOL_INPUT\" | jq -r \".file_path // .path // empty\" 2>/dev/null); if [[ \"$FILE\" == *.xslt || \"$FILE\" == *.xsl ]]; then PORT=$(cat ~/.xslt-debugger-port 2>/dev/null); if [[ -n \"$PORT\" ]]; then curl -s -X POST \"http://127.0.0.1:$PORT/run-transform\" -H \"Content-Type: application/json\" -d \"{\\\"stylesheet\\\": \\\"$FILE\\\", \\\"xml\\\": \\\"${FILE%.*}-input.xml\\\"}\"; fi; fi'",
            "timeout": 60
          }
        ]
      }
    ]
  }
}
```

The hook derives the XML path as `<stylesheet-stem>-input.xml` alongside the stylesheet. If that file doesn't exist, the server returns 404 and `curl` produces no output — Claude receives no feedback for that edit.

## Manual Testing

```bash
# 1. Verify port file exists after VS Code reload
cat ~/.xslt-debugger-port

# 2. Trigger a transform manually
PORT=$(cat ~/.xslt-debugger-port)
curl -s -X POST "http://127.0.0.1:$PORT/run-transform" \
  -H "Content-Type: application/json" \
  -d '{"stylesheet": "/path/to/file.xslt", "xml": "/path/to/input.xml"}'

# 3. Verify error responses
curl -s -X POST "http://127.0.0.1:$PORT/run-transform" \
  -H "Content-Type: application/json" \
  -d '{"stylesheet": "/nonexistent.xslt", "xml": "/nonexistent.xml"}'
# → {"error":"stylesheet or xml file not found"}

# 4. Verify GET returns 404
curl -s -X GET "http://127.0.0.1:$PORT/run-transform"
# → {"error":"not found"}
```

## Output

Transform output and trace logs are merged and returned as plain text. The Output panel ("XSLT Transform") shows the same content in real time while the response is buffered.

Example response body:
```
[trace] Saxon.Start: entered
[trace] Stylesheet compiled successfully.
<?xml version="1.0"?>
<result>...</result>
[xslt] exit code: 0
```
