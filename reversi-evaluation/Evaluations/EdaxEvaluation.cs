using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace reversi_evaluation.Evaluations;

public sealed class EdaxEvaluation : IAsyncDisposable
{
    private static readonly Regex ScoreRegex =
        new(@"(?<!\S)(?<score>[+-]\d+)(?!\S)", RegexOptions.Compiled);

    private readonly string _executablePath;
    private readonly string _workingDirectory;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    
    private Process? _process;
    private StreamWriter? _stdin;
    private Channel<string>? _stdoutChannel;
    
    private bool _disposed;
    private int _currentLevel = 21; // Default level

    private EdaxEvaluation(string executablePath, string workingDirectory)
    {
        _executablePath = executablePath;
        _workingDirectory = workingDirectory;
    }

    public static async Task<EdaxEvaluation> StartAsync(
        string executablePath,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var evaluator = new EdaxEvaluation(executablePath, workingDirectory ?? AppContext.BaseDirectory);
        await evaluator.RestartProcessAsync(cancellationToken).ConfigureAwait(false);
        return evaluator;
    }

    public async Task SetLevelAsync(int level, CancellationToken cancellationToken = default)
    {
        _currentLevel = level;
        await ExecuteCommandAsync($"level {level}", cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetHintScoreAsync(
        string edaxBoardString,
        int hintDepth = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(edaxBoardString))
        {
            throw new ArgumentException("Board string is empty.", nameof(edaxBoardString));
        }

        int maxRestarts = 3;
        int maxRetries = 5;

        for (int restartCount = 0; restartCount < maxRestarts; restartCount++)
        {
            for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            {
                try
                {
                    // If this is a retry, try to return to a clean, recoverable state
                    if (retryCount > 0)
                    {
                        Console.WriteLine($"Retry {retryCount} to get score. Clearing Edax state...");
                        // Sending an empty line returns the prompt ">"
                        await ExecuteCommandAsync("", cancellationToken).ConfigureAwait(false);
                        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                    }

                    await ExecuteCommandAsync($"setboard {edaxBoardString}", cancellationToken).ConfigureAwait(false);
                    var lines = await ExecuteCommandAsync($"hint {hintDepth}", cancellationToken).ConfigureAwait(false);

                    foreach (var line in lines)
                    {
                        var m = ScoreRegex.Match(line);
                        if (m.Success && int.TryParse(m.Groups["score"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int score))
                        {
                            return score; // Success!
                        }
                    }
                    
                    Console.WriteLine("Could not find the score in the Edax output.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during Edax hint: {ex.Message}");
                }
            }

            // If 5 retries fail, we restart the process
            if (restartCount < maxRestarts - 1)
            {
                Console.WriteLine($"Restarting Edax process (Restart {restartCount + 1}/{maxRestarts - 1})...");
                await RestartProcessAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return int.MinValue;
    }

    private async Task RestartProcessAsync(CancellationToken cancellationToken)
    {
        await StopProcessAsync().ConfigureAwait(false);

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                WorkingDirectory = _workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Edax process could not be started.");
        }

        _stdoutChannel = Channel.CreateUnbounded<string>();
        _stdin = _process.StandardInput;

        StartReaders(_process.StandardOutput, _stdoutChannel.Writer);

        // Wait for the first ">" prompt to make sure Edax is ready
        await ReadResponseAsync(cancellationToken).ConfigureAwait(false);

        // Restore the level settings on the new process
        await ExecuteCommandAsync($"level {_currentLevel}", cancellationToken).ConfigureAwait(false);
    }

    private async Task StopProcessAsync()
    {
        if (_process != null)
        {
            try
            {
                if (!_process.HasExited && _stdin != null)
                {
                    await _stdin.WriteLineAsync("quit").ConfigureAwait(false);
                    await _stdin.FlushAsync().ConfigureAwait(false);
                    _process.WaitForExit(500); // Wait briefly to close naturally
                }
            }
            catch { }

            if (!_process.HasExited)
            {
                try { _process.Kill(); } catch { }
            }

            _stdin?.Dispose();
            _process.Dispose();
        }
    }

    private void StartReaders(StreamReader reader, ChannelWriter<string> writer)
    {
        _ = Task.Run(async () =>
        {
            var buffer = new char[1024];
            var currentLine = new StringBuilder();

            try
            {
                while (true)
                {
                    int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (bytesRead == 0) break; // Stream is closed

                    for (int i = 0; i < bytesRead; i++)
                    {
                        char c = buffer[i];

                        if (c == '\n')
                        {
                            await writer.WriteAsync(currentLine.ToString()).ConfigureAwait(false);
                            currentLine.Clear();
                        }
                        else if (c != '\r')
                        {
                            currentLine.Append(c);
                            string text = currentLine.ToString();
                            
                            if (text == ">" || text == "> ")
                            {
                                await writer.WriteAsync(">").ConfigureAwait(false);
                                currentLine.Clear();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors if the process is killed or disposed
            }
            finally
            {
                writer.TryComplete();
            }
        });
    }

    private async Task<IReadOnlyList<string>> ExecuteCommandAsync(string command, CancellationToken cancellationToken)
    {
        EnsureAlive();

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stdin!.WriteLineAsync(command).ConfigureAwait(false);
            await _stdin.FlushAsync().ConfigureAwait(false);
            return await ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<IReadOnlyList<string>> ReadResponseAsync(CancellationToken cancellationToken)
    {
        var lines = new List<string>();

        // Add a 10-second timeout. If Edax freezes, this prevents the program from waiting forever.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            while (true)
            {
                string line = await _stdoutChannel!.Reader.ReadAsync(timeoutCts.Token).ConfigureAwait(false);

                if (line == ">") break; // Prompt found

                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Edax did not respond in 10 seconds.");
        }

        return lines;
    }

    private void EnsureAlive()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EdaxEvaluation));
        if (_process == null || _process.HasExited) throw new InvalidOperationException("Edax process is not running.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopProcessAsync().ConfigureAwait(false);
        _ioLock.Dispose();
    }
}

public static class WinRateCalculator
{
    public static double ToWinRate(int score, int empties)
    {
        double k = 24.0 - (60 - empties) * 0.25;
        k = Math.Max(k, 8);

        double winRate = 100.0 / (1.0 + Math.Exp(-score / k));

        return winRate;
    }    
}