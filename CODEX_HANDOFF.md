# Codex handoff: first Windows validation pass

Open PowerShell in this repository and start Codex there. Give Codex the task below.

```text
You are validating DevNet Doctor v0.1 on a real Windows machine.

Goals:
1. Read README.md and docs/ARCHITECTURE.md first.
2. Do not weaken the privacy/safety rules: never read auth.json contents, never collect tokens/cookies/OAuth codes, and do not permanently hard-code the current proxy port by default.
3. Check installed SDK with `dotnet --info`. If .NET 10 SDK is absent, report that clearly before changing project targets.
4. Run `./scripts/build.ps1`.
5. Fix every compile error and warning that indicates a real bug.
6. Run `DevNetDoctor.SelfTest` until all tests pass.
7. Run the GUI and compare its results with these manual PowerShell checks:
   - WinINET proxy registry values
   - `netsh winhttp show proxy`
   - process/user HTTP_PROXY and HTTPS_PROXY
   - listening state of the system proxy port
   - `codex --version`
   - `codex login status`
8. Validate the OpenAI OAuth route matrix without sending credentials. The intended probe sends only `grant_type=test`.
9. Confirm the Codex repair action:
   - makes a timestamped backup of config.toml
   - preserves existing settings and existing [features] keys
   - adds or changes only `respect_system_proxy = true`
   - remains idempotent when run twice
10. Confirm the shareable report contains no secrets. Add tests for any redaction gap you find.
11. Run `./scripts/publish.ps1 -Runtime win-x64` and confirm a self-contained single-file DevNetDoctor.exe is produced.
12. Do not commit generated bin/, obj/, or artifacts/ folders.

After validation, summarize:
- changes made
- test results
- publish result and EXE path
- any behavior that still needs manual verification
```

## Optional real-world regression case

A useful regression setup is a Windows machine where:

- Windows system proxy is a live local endpoint such as `127.0.0.1:<current-port>`;
- process proxy variables are empty or point at an old port;
- direct access and explicit proxy access to the same service behave differently;
- Codex is installed.

The GUI should explain the mismatch without automatically writing a permanent proxy environment variable.
