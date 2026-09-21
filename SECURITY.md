# Security policy

DevNet Doctor deals with authentication-adjacent network diagnostics, so secret handling is part of the product boundary.

## The tool must never

- read or upload the contents of Codex `auth.json`;
- collect access tokens, refresh tokens, browser cookies, passwords, OAuth authorization codes, or API keys;
- silently change system-wide proxy configuration;
- silently persist a local proxy port into user environment variables;
- disable TLS certificate validation;
- install root certificates;
- modify third-party application provider configuration without an explicit, reviewed adapter.

## Reports

Reports are intended to be shareable, but users should still review them before posting publicly. Report generation runs through a best-effort secret redactor; no redactor can guarantee removal of every possible private string.

## Reporting a vulnerability

Open a private security report on the repository host when available. Do not paste real tokens or authentication files into a public issue.
