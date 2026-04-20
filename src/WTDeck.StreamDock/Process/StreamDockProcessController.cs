using System.Diagnostics;
using Microsoft.Extensions.Logging;
using WTDeck.StreamDock.Configuration;

namespace WTDeck.StreamDock.Process;

public sealed class StreamDockProcessController
{
    private const string ProcessName = "Stream Controller";
    private readonly ILogger<StreamDockProcessController> _logger;

    public StreamDockProcessController(ILogger<StreamDockProcessController> logger)
    {
        _logger = logger;
    }

    public bool IsRunning() =>
        System.Diagnostics.Process.GetProcessesByName(ProcessName).Length > 0;

    public async Task StopAsync(CancellationToken ct)
    {
        var processes = System.Diagnostics.Process.GetProcessesByName(ProcessName);
        if (processes.Length == 0)
        {
            _logger.LogDebug("Stream Controller is not running");
            return;
        }

        _logger.LogInformation("Stopping {Count} Stream Controller process(es)", processes.Length);

        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1500))
                    {
                        _logger.LogDebug("Stream Controller did not close gracefully, killing");
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop Stream Controller process {Id}", process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        // Give the OS a moment to release file handles
        await Task.Delay(300, ct);
    }

    public Task StartAsync(StreamDockPaths paths, CancellationToken ct)
    {
        if (paths.ExecutablePath is null || !File.Exists(paths.ExecutablePath))
        {
            _logger.LogWarning("Stream Controller executable not found; cannot start");
            return Task.CompletedTask;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = paths.ExecutablePath,
                WorkingDirectory = paths.InstallDir ?? "",
                UseShellExecute = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process is not null)
            {
                _logger.LogInformation("Stream Controller started (pid {Pid})", process.Id);
                process.Dispose();
            }
            else
            {
                _logger.LogWarning("Stream Controller did not start");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Stream Controller");
        }

        return Task.CompletedTask;
    }

    public async Task RestartAsync(StreamDockPaths paths, CancellationToken ct)
    {
        await StopAsync(ct);
        await Task.Delay(500, ct);
        await StartAsync(paths, ct);
    }
}
