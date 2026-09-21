# Related public reports to test against

These are examples of public reports with overlapping symptoms. They are not proof that every report has the same root cause. Only reply when DevNet Doctor produces evidence that is relevant to that issue.

## Codex authentication / token exchange

- openai/codex #37467 — Windows/WSL browser authentication succeeds, subsequent token exchange fails.
  https://github.com/openai/codex/issues/37467
- openai/codex #36490 — Windows CLI `token_exchange_failed` while browser/curl connectivity appears normal.
  https://github.com/openai/codex/issues/36490
- openai/codex #30746 — Windows Desktop browser OAuth succeeds, Desktop token exchange fails.
  https://github.com/openai/codex/issues/30746
- openai/codex #37682 — Windows/WSL browser callback reachable but successful auth path hangs around the handoff/token-exchange stage.
  https://github.com/openai/codex/issues/37682

## Proxy inheritance / transport

- openai/codex #20844 — Windows Codex App/CLI unstable through SOCKS; explicit HTTP_PROXY/HTTPS_PROXY improves behavior.
  https://github.com/openai/codex/issues/20844
- openai/codex #29958 — Windows WebSocket transport differs between `respect_system_proxy` and explicit HTTP_PROXY/HTTPS_PROXY.
  https://github.com/openai/codex/issues/29958
- openai/codex #15447 — Codex Desktop does not pass Windows system proxy cleanly into a WSL-backed Codex process.
  https://github.com/openai/codex/issues/15447

## Broader system-proxy edge cases

- openai/codex #45916 — some Codex feature traffic can bypass the system/PAC route even with system-proxy support enabled.
  https://github.com/openai/codex/issues/45916

## Community etiquette

Do not mass-post the project link. First compare the issue's symptoms with a DevNet Doctor route matrix. If the tool does not reproduce a route mismatch, say so rather than presenting it as a fix.
