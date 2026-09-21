# Community reply template

Use this only on issues where the symptoms actually match. Disclose that you are a contributor; do not spam unrelated threads.

> I ran into a very similar Windows failure: browser sign-in completed, but the local client failed later during token exchange / network requests. In my case, Windows had a working local system proxy while the CLI/client used a different route (or a stale proxy port).
>
> I helped build an open-source diagnostic tool called **DevNet Doctor** to make this specific class of problem easier to verify. It compares direct, Windows-system-proxy, and environment-proxy routes; checks local proxy listeners and stale ports; and has a conservative Codex check for `respect_system_proxy`. It does not read `auth.json` contents or collect tokens.
>
> If your symptoms match, the redacted route matrix/report may help confirm whether this is the same root cause. Repository: <REPOSITORY_URL>
>
> If the tool says all routes are equivalent, then your issue is probably a different bug and this workaround may not apply.

Before posting:

1. Replace `<REPOSITORY_URL>` with the public repository.
2. Link to the exact release/source commit you tested.
3. State the DevNet Doctor version.
4. Do not imply it is an official OpenAI tool.
5. Do not tell users to upload `auth.json` or tokens.
