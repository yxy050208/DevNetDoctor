# Changelog

## 0.1.1 - 2026-09-22

- Added direct IPv4 and IPv6 route probes.
- Added a transport-only `HEAD` probe for the Codex device-auth endpoint; no device login is attempted.
- Added TLS certificate subject, issuer, fingerprint, chain status, HTTP version, and peer metadata to reports.
- Added conservative TLS validation failure and route-difference findings without disabling certificate checks.
- Added Codex Desktop package version discovery when available and clarified the distinction between network reachability and Codex application-layer failures.
- Tightened OAuth classification to require a structured OAuth error instead of matching arbitrary response text.

## 0.1.0 - 2026-09-21

Initial implementation.

- Windows system / environment / WinHTTP proxy inspection.
- Direct vs system-proxy vs environment-proxy OpenAI OAuth route matrix.
- Local proxy listener and stale-port detection.
- Codex CLI/auth-metadata/config inspection.
- Safe `respect_system_proxy = true` repair with timestamped backup.
- Codex stale-process stop action with confirmation.
- CCSwitch process, listening-port, and common config-path discovery.
- Redacted shareable diagnostic report.
- WinForms GUI and PowerShell 5.1-friendly portable scanner.
- Dependency-free self-test harness.
