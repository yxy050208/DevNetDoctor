using System.Diagnostics;
using System.Text;

namespace DevNetDoctor.Core.Services;

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public string Combined => string.Join(Environment.NewLine, new[] { StdOut, StdErr }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public static class CommandRunner
{
    public static async Task<CommandResult> RunAsync(string fileName, string arguments, int timeoutMs = 8000, CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        try
        {
            if (!process.Start()) return new(-1, string.Empty, "Process did not start.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new(-2, await SafeAwait(stdoutTask), await SafeAwait(stderrTask) + Environment.NewLine + "Timed out.");
            }

            return new(process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (Exception ex)
        {
            return new(-1, string.Empty, ex.Message);
        }
    }

    private static async Task<string> SafeAwait(Task<string> task)
    {
        try { return await task; } catch { return string.Empty; }
    }
}
