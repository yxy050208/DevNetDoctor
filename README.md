# DevNet Doctor

**浏览器能上网，Codex 或命令行工具却连不上？DevNet Doctor 帮你排查 Windows 上的代理配置和网络路径问题。**

它是一款在本机运行的 Windows 诊断工具。v0.1 重点检查系统代理、环境变量代理、本地端口和 OpenAI OAuth 连通性，并提供 Codex 配置检查与有限的修复操作。

## 解决什么问题？

| 你遇到的现象 | 工具帮你检查什么 |
| --- | --- |
| 浏览器可以访问服务，Codex / CLI 却连接失败 | 系统代理与当前进程的代理环境变量是否一致；直连和显式代理路径的结果有何不同 |
| 重启电脑或代理软件后，原本可用的开发工具突然失联 | `HTTP_PROXY` / `HTTPS_PROXY` 是否仍指向旧端口，该本地端口是否还在监听 |
| 浏览器登录完成，但 CLI 的登录或 token exchange 失败 | 用不带凭据的测试请求检查 OAuth 服务是否可达，辅助区分网络路径故障与后续登录问题 |
| 不确定 Codex 是否配置了系统代理支持 | 检查 `config.toml` 中的 `respect_system_proxy`，在用户确认后备份并启用该设置 |
| 使用 CCSwitch 时，不清楚本地转发端口与系统代理的关系 | 查看 CCSwitch 进程、监听端口和常见配置文件位置 |
| 向别人求助时，不知道该提供哪些排查信息 | 生成包含代理来源、路径测试和诊断结论的脱敏报告 |

**典型例子：** Windows 系统代理已经改成 `127.0.0.1:22626`，但 `HTTPS_PROXY` 仍然指向已关闭的 `127.0.0.1:21968`。浏览器走当前系统代理，开发工具却继续连接旧端口。DevNet Doctor 会检查这类配置不一致和失效端口，帮助你确定该调整哪里。

这些现象可能还有其他原因。工具提供诊断证据和建议，并不保证所有连接或登录错误都能一键修复。

## 下载和使用

1. 从 [v0.1.0 发布页](https://github.com/yxy050208/DevNetDoctor/releases/tag/v0.1.0) 下载 **DevNetDoctor.exe**。
2. 在 Windows x64 上运行。EXE 自带运行时，无需另外安装 .NET SDK。
3. 先查看 **Findings**（诊断结论），再查看 **Route matrix**（网络路径对比）。
4. 如需让支持该设置的 Codex 版本使用系统代理，可在 **Codex** 页确认后执行 **Enable Codex system proxy**；修改前会备份配置。
5. 求助时可导出 **Shareable report**，发布前仍需检查报告中是否包含不希望公开的信息。

只想运行脚本，也可以下载源码包，按下方 PowerShell 快速开始操作。

## v0.1 能做什么、有哪些边界？

- **诊断为主：** 比较 WinINET、WinHTTP 和环境变量中的代理信息；网络路径测试目前专门针对 OpenAI OAuth，不代表 Git、npm、pip 等所有服务都可达。
- **有限修复：** 可备份并设置 Codex 的 `respect_system_proxy = true`。该设置是否生效取决于 Codex 版本；不会自动修复账号、令牌、服务故障或所有代理问题。
- **避免留下失效配置：** 不会把当前本地代理端口永久写入用户环境变量。GUI 可复制仅作用于当前 PowerShell 会话的代理命令。
- **CCSwitch 只检查：** 不修改 provider 配置；PAC 只报告地址，不执行脚本。
- **凭据保护：** DevNet Doctor 只检查 `auth.json` 的文件元数据。OAuth 探测只发送 `grant_type=test`，不发送账号凭据。报告会过滤常见敏感字段。
- **网络探测会联网：** 配置检查在本机进行，连通性测试会请求 OpenAI OAuth 端点；工具不用于绕过服务可用性或地区限制。

---

A local-first Windows diagnostic and repair tool for cases where **the browser works but a CLI or desktop developer tool does not**. The first release focuses on proxy inheritance, stale local proxy ports, OAuth connectivity, and Codex/CCSwitch diagnostics. English technical details follow.

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

v0.1.0 has passed a local Windows build with .NET SDK 10.0.400 (zero warnings and errors), the C# self-test harness, and a PowerShell 5.1 scanner/report smoke test. A self-contained win-x64 EXE was produced and passed a process-start smoke test.

This is an initial release. Full GUI interaction testing, every repair edge case, and behavior across different Windows, Codex and proxy configurations still need broader validation. `CODEX_HANDOFF.md` records the original validation plan; it is not a claim that every item has been completed.

## License

MIT. See `LICENSE`.
