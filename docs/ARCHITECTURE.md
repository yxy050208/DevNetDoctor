# Architecture

DevNet Doctor uses a small adapter architecture so network diagnostics stay generic while individual developer tools can add targeted checks.

## Core flow

```text
WindowsProxyInspector
        │
        ├── WinINET manual proxy / PAC metadata
        ├── environment proxy variables
        └── WinHTTP state
        │
        v
NetworkProbeService
        │
        ├── Direct
        ├── Explicit Windows proxy
        └── Explicit environment proxy
        │
        v
DiagnosticEngine
        │
        ├── route divergence
        ├── closed local proxy port
        ├── stale/mismatched proxy port
        ├── Codex findings
        └── CCSwitch findings
        │
        v
GUI + redacted report
```

The important design rule is **observe before mutate**.

## Why explicit route tests?

A test that merely calls `HttpClient` with defaults can hide the exact question we need to answer: which proxy source did the process actually use? v0.1.1 therefore constructs explicit Direct / Windows-system-proxy / environment-proxy clients and separately tests direct IPv4 and IPv6.

The specialized OpenAI adapter intentionally sends an invalid OAuth grant (`grant_type=test`) without credentials. Receiving a structured OAuth validation error demonstrates that the request reached the service far enough to be parsed. Device-auth uses HEAD only, so it checks transport without starting device authorization. Neither is an authentication attempt.

TLS validation remains enabled. The probe records certificate metadata and reports policy errors or route issuer differences as evidence, but it does not identify a security product from a certificate alone and never disables verification.

## Repairs

Repairs are adapter-specific and conservative.

### Codex

The only automatic config mutation in v0.1 is:

```toml
[features]
respect_system_proxy = true
```

Before editing, DevNet Doctor creates a timestamped copy of `config.toml`. Existing feature keys are preserved.

### Environment proxy variables

v0.1 does not permanently synchronize them. A local proxy application may allocate a different loopback port after reboot, turning a previously correct permanent value into a stale setting. The GUI instead provides temporary PowerShell commands for the current shell.

### CCSwitch

CCSwitch variants and versions use different configuration layouts. v0.1 detects processes, listening ports, and likely config files but makes no provider changes.

## Privacy boundary

`auth.json` is checked with file metadata APIs only. No file read is performed.

Shareable reports pass through `SecretRedactor`, which removes common Bearer tokens, token-like JSON fields, Cookie/Set-Cookie headers, JWTs, and OAuth query codes.

## Future adapters

Good candidates:

- Git
- npm / pnpm
- pip / uv
- Cargo
- VS Code extension host
- WSL proxy bridging
- generic HTTPS/SSE/WebSocket profiles

Each adapter should add diagnosis only when it can state the evidence and the exact configuration source involved.
