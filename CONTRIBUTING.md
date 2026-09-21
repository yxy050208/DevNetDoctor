# Contributing

Contributions are welcome, especially reproducible Windows network cases.

A useful bug report includes:

- Windows version;
- DevNet Doctor version;
- affected application and version;
- whether a manual Windows proxy or PAC is configured;
- the **redacted** DevNet Doctor report;
- what works (browser, curl, explicit proxy) and what fails.

Never attach `auth.json`, access/refresh tokens, cookies, or OAuth codes.

For new repair actions, follow three rules:

1. diagnosis must precede mutation;
2. back up configuration before editing;
3. make the smallest application-specific change possible and provide a rollback path.
