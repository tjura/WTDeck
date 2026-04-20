namespace WTDeck.App.Debug;

public sealed class DebugRunState
{
    private readonly object _lock = new();
    private readonly List<string> _failures = [];
    private int _exitCode;
    private bool _telemetryGatePassed = true;
    private bool _uiGatePassed = true;

    public int ExitCode => Volatile.Read(ref _exitCode);

    public bool TelemetryGatePassed
    {
        get
        {
            lock (_lock)
                return _telemetryGatePassed;
        }
    }

    public bool UiGatePassed
    {
        get
        {
            lock (_lock)
                return _uiGatePassed;
        }
    }

    public int FailureCount
    {
        get
        {
            lock (_lock)
                return _failures.Count;
        }
    }

    public void FailTelemetry(string message)
    {
        lock (_lock)
        {
            _telemetryGatePassed = false;
            _failures.Add(message);
            _exitCode = 1;
        }
    }

    public void FailUi(string message)
    {
        lock (_lock)
        {
            _uiGatePassed = false;
            _failures.Add(message);
            _exitCode = 1;
        }
    }
}
