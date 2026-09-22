using System.Diagnostics;
using DevNetDoctor.Core.Models;
using DevNetDoctor.Core.Services;

namespace DevNetDoctor.App;

public sealed class MainForm : Form
{
    private readonly DiagnosticEngine _engine = new();
    private readonly CodexAdapter _codex = new();
    private DiagnosticSnapshot? _snapshot;

    private readonly Button _scanButton = new() { Text = "Scan now", AutoSize = true };
    private readonly Label _statusLabel = new() { Text = "Ready", AutoSize = true };
    private readonly DataGridView _findingsGrid = CreateGrid();
    private readonly DataGridView _routeGrid = CreateGrid();
    private readonly TextBox _codexBox = CreateTextBox();
    private readonly TextBox _ccSwitchBox = CreateTextBox();
    private readonly TextBox _reportBox = CreateTextBox();
    private readonly Button _enableCodexProxyButton = new() { Text = "Enable Codex system proxy", AutoSize = true };
    private readonly Button _stopCodexButton = new() { Text = "Stop Codex processes", AutoSize = true };
    private readonly Button _openConfigButton = new() { Text = "Open config.toml", AutoSize = true };
    private readonly Button _copySessionProxyButton = new() { Text = "Copy temporary proxy commands", AutoSize = true };
    private readonly Button _copyReportButton = new() { Text = "Copy report", AutoSize = true };
    private readonly Button _saveReportButton = new() { Text = "Save report", AutoSize = true };

    public MainForm()
    {
        Text = "DevNet Doctor 0.1.1";
        Width = 1120;
        Height = 760;
        MinimumSize = new Size(860, 600);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        WireEvents();
        Shown += async (_, _) => await ScanAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 8)
        };
        var title = new Label
        {
            Text = "DevNet Doctor",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 4, 16, 0)
        };
        var subtitle = new Label
        {
            Text = "Windows developer network diagnostics — scan first, repair only with approval",
            AutoSize = true,
            Margin = new Padding(0, 9, 20, 0)
        };
        _scanButton.Margin = new Padding(0, 4, 12, 0);
        _statusLabel.Margin = new Padding(0, 9, 0, 0);
        header.Controls.AddRange(new Control[] { title, subtitle, _scanButton, _statusLabel });

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildFindingsTab());
        tabs.TabPages.Add(BuildRoutesTab());
        tabs.TabPages.Add(BuildCodexTab());
        tabs.TabPages.Add(BuildCcSwitchTab());
        tabs.TabPages.Add(BuildReportTab());

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(tabs, 0, 1);
        Controls.Add(root);
    }

    private TabPage BuildFindingsTab()
    {
        var page = new TabPage("Findings");
        _findingsGrid.Columns.Add("Severity", "Severity");
        _findingsGrid.Columns.Add("Title", "Finding");
        _findingsGrid.Columns.Add("Detail", "Detail");
        _findingsGrid.Columns.Add("Action", "Suggested action");
        _findingsGrid.Columns[0].Width = 90;
        _findingsGrid.Columns[1].Width = 230;
        _findingsGrid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _findingsGrid.Columns[3].Width = 300;
        page.Controls.Add(_findingsGrid);
        return page;
    }

    private TabPage BuildRoutesTab()
    {
        var page = new TabPage("Route matrix");
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1000, 0),
            Text = "Windows/.NET probes: OAuth uses grant_type=test; device-auth uses HEAD (transport only, no login). Direct IPv4/IPv6 still follow VPN/OS routing. Certificate evidence is in Shareable report. These tests do not reproduce Codex or WSL traffic."
        };
        _routeGrid.Columns.Add("Route", "Route");
        _routeGrid.Columns.Add("Endpoint", "Endpoint");
        _routeGrid.Columns.Add("Proxy", "Proxy");
        _routeGrid.Columns.Add("Status", "HTTP");
        _routeGrid.Columns.Add("Class", "Classification");
        _routeGrid.Columns.Add("Elapsed", "Time");
        _routeGrid.Columns.Add("Detail", "Detail");
        _routeGrid.Columns[6].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        container.Controls.Add(note, 0, 0);
        container.Controls.Add(_routeGrid, 0, 1);
        page.Controls.Add(container);
        return page;
    }

    private TabPage BuildCodexTab()
    {
        var page = new TabPage("Codex");
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        buttons.Controls.AddRange(new Control[] { _enableCodexProxyButton, _openConfigButton, _stopCodexButton, _copySessionProxyButton });
        container.Controls.Add(buttons, 0, 0);
        container.Controls.Add(_codexBox, 0, 1);
        page.Controls.Add(container);
        return page;
    }

    private TabPage BuildCcSwitchTab()
    {
        var page = new TabPage("CCSwitch");
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1000, 0),
            Text = "v0.1.1 detects CCSwitch processes, their listening TCP ports, and likely config files. It intentionally does not rewrite provider configuration because CCSwitch variants use different schemas."
        }, 0, 0);
        panel.Controls.Add(_ccSwitchBox, 0, 1);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildReportTab()
    {
        var page = new TabPage("Shareable report");
        var container = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(8) };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.AddRange(new Control[] { _copyReportButton, _saveReportButton });
        container.Controls.Add(buttons, 0, 0);
        container.Controls.Add(_reportBox, 0, 1);
        page.Controls.Add(container);
        return page;
    }

    private void WireEvents()
    {
        _scanButton.Click += async (_, _) => await ScanAsync();
        _enableCodexProxyButton.Click += async (_, _) => await EnableCodexProxyAsync();
        _stopCodexButton.Click += (_, _) => StopCodexProcesses();
        _openConfigButton.Click += (_, _) => OpenCodexConfig();
        _copySessionProxyButton.Click += (_, _) => CopyTemporaryProxyCommands();
        _copyReportButton.Click += (_, _) => CopyReport();
        _saveReportButton.Click += (_, _) => SaveReport();
    }

    private async Task ScanAsync()
    {
        ToggleBusy(true, "Scanning…");
        try
        {
            _snapshot = await _engine.ScanAsync();
            Render(_snapshot);
            _statusLabel.Text = $"Last scan: {_snapshot.Timestamp:T}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Scan failed";
            MessageBox.Show(this, SecretRedactor.Redact(ex.ToString()), "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleBusy(false, _statusLabel.Text);
        }
    }

    private void Render(DiagnosticSnapshot s)
    {
        _findingsGrid.Rows.Clear();
        foreach (var finding in s.Findings.OrderByDescending(f => f.Severity))
            _findingsGrid.Rows.Add(finding.Severity, finding.Title, finding.Detail, finding.SuggestedAction ?? string.Empty);

        _routeGrid.Rows.Clear();
        foreach (var probe in s.Probes)
            _routeGrid.Rows.Add(probe.RouteName, $"{probe.Method} {probe.Endpoint}", probe.Proxy ?? "Direct", probe.HttpStatus?.ToString() ?? "—", probe.Classification, $"{probe.ElapsedMilliseconds} ms", SecretRedactor.Redact($"{probe.Detail} Issuer: {probe.CertificateIssuer ?? "unknown"}; TLS: {probe.TlsPolicyErrors ?? "not observed"}"));

        var c = s.Codex;
        _codexBox.Text = string.Join(Environment.NewLine, new[]
        {
            $"Installed: {c.Installed}",
            $"Version: {c.Version ?? "(unknown)"}",
            $"Desktop package version: {c.DesktopVersion}",
            $"Executable(s): {string.Join(" | ", c.Executables)}",
            $"Codex home: {c.CodexHome}",
            $"Config: {c.ConfigPath} (exists={c.ConfigExists})",
            $"respect_system_proxy: {c.RespectSystemProxy?.ToString() ?? "not set"}",
            $"Login status: {c.LoginStatus}",
            $"auth.json exists: {c.AuthFileExists}",
            $"auth.json last write: {c.AuthFileLastWrite?.ToString("G") ?? "n/a"}",
            $"Running Codex PIDs: {(c.RunningProcessIds.Count == 0 ? "none" : string.Join(", ", c.RunningProcessIds))}",
            "",
            "Security note: DevNet Doctor checks auth.json metadata only; it never reads the file contents."
        });

        var cc = s.CcSwitch;
        _ccSwitchBox.Text = string.Join(Environment.NewLine, new[]
        {
            $"Running: {cc.Running}",
            $"PIDs: {(cc.ProcessIds.Count == 0 ? "none" : string.Join(", ", cc.ProcessIds))}",
            $"Listening ports: {(cc.ListeningPorts.Count == 0 ? "none" : string.Join(", ", cc.ListeningPorts))}",
            "",
            "Candidate config files:",
            cc.CandidateConfigFiles.Count == 0 ? "(none found in common locations)" : string.Join(Environment.NewLine, cc.CandidateConfigFiles)
        });

        _reportBox.Text = DiagnosticReportBuilder.Build(s);
        _enableCodexProxyButton.Enabled = c.Installed && c.RespectSystemProxy != true;
        _openConfigButton.Enabled = c.Installed;
        _stopCodexButton.Enabled = c.RunningProcessIds.Count > 0;
        _copySessionProxyButton.Enabled = (s.SystemProxy.HttpsProxy ?? s.SystemProxy.HttpProxy) is not null;
    }

    private async Task EnableCodexProxyAsync()
    {
        if (_snapshot is null) return;
        var path = _snapshot.Codex.ConfigPath;
        var answer = MessageBox.Show(this,
            $"DevNet Doctor will back up and edit:\n{path}\n\nIt will only set [features] respect_system_proxy = true. Continue?",
            "Enable Codex system proxy", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;

        try
        {
            var backup = _codex.EnableRespectSystemProxy(path);
            MessageBox.Show(this, $"Updated config.toml.\nBackup: {backup}\n\nRestart Codex for the setting to take effect.",
                "Repair complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await ScanAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, SecretRedactor.Redact(ex.Message), "Repair failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StopCodexProcesses()
    {
        var answer = MessageBox.Show(this,
            "This will force-stop running processes whose name contains 'codex'. Unsaved Codex work may be interrupted. Continue?",
            "Stop Codex processes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        var count = _codex.StopCodexProcesses();
        MessageBox.Show(this, $"Stopped {count} Codex process(es).", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        _ = ScanAsync();
    }

    private void OpenCodexConfig()
    {
        if (_snapshot is null) return;
        var path = _snapshot.Codex.ConfigPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) File.WriteAllText(path, string.Empty);
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open config", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyTemporaryProxyCommands()
    {
        if (_snapshot is null) return;
        var proxy = _snapshot.SystemProxy.HttpsProxy ?? _snapshot.SystemProxy.HttpProxy;
        if (proxy is null) return;
        var url = proxy.ToString();
        var text = $"$env:HTTP_PROXY=\"{url}\"{Environment.NewLine}" +
                   $"$env:HTTPS_PROXY=\"{url}\"{Environment.NewLine}" +
                   "$env:NO_PROXY=\"localhost,127.0.0.1,::1\"";
        Clipboard.SetText(text);
        MessageBox.Show(this,
            "Copied temporary PowerShell proxy commands. They affect only the shell session where you run them and do not create a stale permanent port after reboot.",
            "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopyReport()
    {
        if (string.IsNullOrWhiteSpace(_reportBox.Text)) return;
        Clipboard.SetText(_reportBox.Text);
        MessageBox.Show(this, "Redacted diagnostic report copied to the clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveReport()
    {
        if (string.IsNullOrWhiteSpace(_reportBox.Text)) return;
        using var dialog = new SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"devnet-doctor-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            File.WriteAllText(dialog.FileName, _reportBox.Text);
    }

    private void ToggleBusy(bool busy, string status)
    {
        _scanButton.Enabled = !busy;
        UseWaitCursor = busy;
        _statusLabel.Text = status;
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
        DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.True },
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private static TextBox CreateTextBox() => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font("Consolas", 10)
    };
}
