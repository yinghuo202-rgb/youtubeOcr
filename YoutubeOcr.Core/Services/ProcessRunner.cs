using System.Diagnostics;
using System.Text;

namespace YoutubeOcr.Core.Services;

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool IsSuccess => ExitCode == 0;
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);
}

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var stdoutTcs = new TaskCompletionSource();
        var stderrTcs = new TaskCompletionSource();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                stdoutTcs.TrySetResult();
                return;
            }

            stdout.AppendLine(args.Data);
            outputProgress?.Report(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                stderrTcs.TrySetResult();
                return;
            }

            stderr.AppendLine(args.Data);
            outputProgress?.Report(args.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* ignored */ }
        });

        await Task.WhenAll(Task.Run(() => process.WaitForExit(), cancellationToken), stdoutTcs.Task, stderrTcs.Task);

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
