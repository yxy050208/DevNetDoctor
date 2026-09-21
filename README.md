# DevNet Doctor

A lightweight, local-first Windows diagnostic and repair tool for the annoying class of problems where **the browser works but a CLI/desktop developer tool does not**.

The first release focuses on proxy inheritance, stale local proxy ports, OAuth/token-exchange connectivity, and Codex/CCSwitch diagnostics.

## Why this exists

On Windows, different applications may use different proxy sources:

- WinINET / Windows Settings proxy
- WinHTTP proxy
- `HTTP_PROXY`, `HTTPS_PROXY`, `ALL_PROXY`, `NO_PROXY`
- PAC/WPAD
- application-specific proxy discovery
- a local forwarding application whose port can change after reboot

That can produce failures such as browser login succeeding while a CLI token exchange fails, or a tool continuing to use yesterday's dead local proxy port.

DevNet Doctor compares the routes instead of guessing.

## v0.1 features

- Reads Windows WinINET manual proxy and PAC URL.
- Reads process-level and user-level proxy environment variables.
- Shows WinHTTP proxy state.
- Detects whether a loopback proxy port is actually listening.
- Builds an OpenAI OAuth route matrix for:
  - direct connection
  - Windows system proxy
  - environment proxy
- Detects the useful distinction between:
  - region/network policy rejection on one route
  - successful arrival at the OAuth service on another route
- Detects stale/mismatched local proxy ports.
- Codex adapter:
  - `codex --version`
  - `codex login status`
  - resolves `CODEX_HOME`
  - checks `config.toml`
  - checks only `auth.json` metadata (never contents)
  - safely enables `[features] respect_system_proxy = true`
  - makes a timestamped backup before editing
  - can stop stale Codex processes after explicit confirmation
- CCSwitch adapter:
  - process detection
  - listening TCP port detection
  - common config-file discovery
  - no provider-file mutation in v0.1
- Redacted shareable diagnostics report.
- Portable PowerShell edition for immediate testing without compiling the GUI.

## Safety and privacy

DevNet Doctor is intentionally conservative.

- Scan is the default action.
- It never reads the contents of Codex `auth.json`.
- It never asks for or sends account tokens, passwords, cookies, or OAuth authorization codes.
- The OpenAI OAuth connectivity probe sends only `grant_type=test`. A `400` invalid-value response is treated as evidence that the OAuth service was reached.
- The report redactor filters common Bearer tokens, JSON token fields, cookies, JWTs, and OAuth codes.
- It does **not** permanently set `HTTP_PROXY` / `HTTPS_PROXY` to the current local port, because local proxy apps may change ports after reboot.
- Codex configuration is backed up before modification.
- CCSwitch provider configuration is not modified in v0.1.
- The tool is for diagnosing connectivity to services you are authorized to access; it is not intended to bypass service availability or regional restrictions.

## Quick start — portable PowerShell edition

Run a normal scan:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\DevNetDoctor.ps1
```

If the scan shows Codex is installed, Windows has a working system proxy, and `respect_system_proxy` is absent:

```powershell
.\DevNetDoctor.ps1 -RepairCodexProxy
```

Save a compact report:

```powershell
.\DevNetDoctor.ps1 -ReportPath .\devnet-report.txt
```

## Build the Windows GUI

Requirements for development:

- Windows 10/11
- .NET 10 SDK

```powershell
.\scripts\build.ps1
```

Publish a self-contained single-file x64 build:

```powershell
.\scripts\publish.ps1 -Runtime win-x64
```

Output:

```text
artifacts\win-x64\DevNetDoctor.exe
```

Microsoft documents self-contained single-file deployment with `dotnet publish -r <RID>` and `PublishSingleFile=true`.

## UI workflow

1. Launch DevNet Doctor.
2. It scans automatically.
3. Read **Findings** first.
4. Use **Route matrix** to compare direct/system/environment routes.
5. If Codex needs the system-proxy feature, use **Enable Codex system proxy**. A backup is made first.
6. If a running Codex Desktop backend is stale, use **Stop Codex processes**, then start Codex again.
7. Use **Shareable report** when asking for help or filing an issue.

## Typical diagnosis

A pattern like this is very informative:

```text
Direct                 403  RegionBlocked
Windows system proxy   400  OAuthReachable
```

This does not mean a token is invalid. It means the two network routes reach the endpoint differently. If the affected application is expected to use the configured proxy, inspect whether it actually inherits/respects that proxy.

Another common pattern:

```text
Windows system proxy: 127.0.0.1:22626
HTTPS_PROXY:           127.0.0.1:21968
```

If `21968` is no longer listening, the environment variable is stale. This commonly happens when a local proxy app changes its port after reboot.

## Project layout

```text
DevNetDoctor/
├─ DevNetDoctor.ps1                 # zero-dependency portable scanner
├─ src/
│  ├─ DevNetDoctor.Core/            # diagnostics / repair engine
│  ├─ DevNetDoctor.App/             # WinForms GUI
│  └─ DevNetDoctor.SelfTest/        # dependency-free test harness
├─ scripts/
│  ├─ build.ps1
│  ├─ publish.ps1
│  └─ clean.ps1
└─ docs/
```

## Current limitations

- v0.1 reports PAC URLs but does not execute PAC JavaScript itself.
- The route matrix currently includes a specialized OpenAI OAuth probe because Codex is the first adapter. More generic endpoint profiles can be added next.
- CCSwitch has multiple variants/config formats, so v0.1 avoids rewriting provider settings.
- SOCKS-specific behavior is reported indirectly; the C# route tester currently uses HTTP(S) proxy endpoints.

## Development status

This repository was generated as a first working implementation. The source environment used to assemble it did not contain the .NET SDK, so the project includes a self-test harness and build scripts but still needs its first Windows `dotnet build`/`dotnet run` pass. See `CODEX_HANDOFF.md` for a ready-made local Codex validation task.

## License

MIT. See `LICENSE`.
