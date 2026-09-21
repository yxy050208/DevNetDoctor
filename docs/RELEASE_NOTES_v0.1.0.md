# DevNet Doctor v0.1.0 — Windows 开发工具代理诊断

**解决的核心问题：浏览器能上网，但 Codex 或命令行开发工具却连不上。**

Windows 上不同程序可能读取不同的代理配置。系统代理已经更新，终端却仍使用旧的 `HTTPS_PROXY`；代理软件重启后端口改变，开发工具还在连接已关闭的端口。这些不一致会表现为连接失败、超时，或者浏览器登录完成后 CLI 的 token exchange 失败。

DevNet Doctor 将系统代理、环境变量、端口监听状态和网络路径测试放在一起，帮助定位问题来源。v0.1 重点支持 Codex 和 CCSwitch 的诊断。

## 本版功能

- 检查 Windows WinINET、WinHTTP，以及进程和用户层面的代理环境变量。
- 检测本地代理端口关闭、系统代理与环境变量不一致等常见问题。
- 对比直连、系统代理和环境代理到 OpenAI OAuth 端点的测试结果。探测只发送 `grant_type=test`，不发送账号凭据。
- 检查 Codex 版本、登录状态和配置；可在备份后启用 `respect_system_proxy = true`。
- 检测 CCSwitch 进程、监听端口和常见配置文件位置。
- 生成脱敏诊断报告，方便求助。

## 下载哪个文件？

- **DevNetDoctor.exe**：Windows x64 图形界面版，自带 .NET 运行时，下载后运行。
- **DevNetDoctor-v0.1.0-source.zip**：源码包，含便携 PowerShell 扫描器 `DevNetDoctor.ps1`。

启动 GUI 后先看 **Findings**，再看 **Route matrix**。修复 Codex 前会提示确认并备份配置。

## 验证情况与范围

本机 Windows 验证已通过：.NET 10 编译（零错误、零警告）、C# 自测、PowerShell 5.1 扫描与报告冒烟测试，以及自包含 EXE 的进程启动检查。完整 GUI 交互和不同机器上的兼容性仍需进一步验证。

这是首个版本，主要用于诊断，不保证一键修复所有联网或登录问题。网络路径测试目前专门针对 OpenAI OAuth；CCSwitch 不修改 provider 配置，PAC 只报告地址。不会永久写入本地代理端口，也不会读取 `auth.json` 的内容。报告公开前请检查是否有不希望分享的信息。

EXE SHA-256：`B9C95D1E26292945738A015B1193D6FE58EB0E650229F11245C7D717FBA06876`
