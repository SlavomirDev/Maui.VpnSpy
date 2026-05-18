namespace Maui.VpnSpy;

/// <summary>
/// Default implementation of <see cref="IVpnStateService"/>. Polls the platform
/// <see cref="IVpnDetector"/> periodically and remembers whether VPN was ever seen
/// active in the current session.
/// </summary>
internal sealed class VpnStateService : IVpnStateService
{
    private const int _CheckIntervalMs = 4000;

    private readonly IVpnDetector _detector;
    private readonly IDispatcher _dispatcher;
    private readonly StateListener<bool?> _listener;
    private bool _disposed;

    /// <summary>
    /// Latest VPN flag, or null until the first sample is taken.
    /// </summary>
    public bool? IsVpnActive => _listener.CurrentState;

    /// <summary>
    /// Raised whenever <see cref="IsVpnActive"/> changes.
    /// </summary>
    public event Action<bool?, bool?> OnChanged
    {
        add => _listener.OnStateChanged += value;
        remove => _listener.OnStateChanged -= value;
    }

    /// <summary>
    /// Wires the listener to the platform detector and starts the polling loop.
    /// </summary>
    public VpnStateService(IVpnDetector detector)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _dispatcher = Dispatcher.GetForCurrentThread()
            ?? throw new InvalidOperationException("No MAUI Dispatcher is available on the current thread. " +
                                                   "Register IVpnStateService after MauiAppBuilder has been configured.");

        _listener = new StateListener<bool?>(
            converter: new AnyTrueConverter(),
            comparer: new NullableBoolComparer(),
            autoStart: false)
        {
            CheckIntervalMs = _CheckIntervalMs
        };

        _listener.AddProcessor(SampleVpnAsync);
        _listener.Start();
    }

    /// <summary>
    /// Forces an immediate poll of the detector.
    /// </summary>
    public void Refresh() => _ = _listener.CheckNow();

    private async Task<bool?> SampleVpnAsync()
    {
        try
        {
            var isActive = await _dispatcher.DispatchAsync(_detector.IsVpnActive);
            return isActive;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Releases the polling listener; the detector itself is owned by DI.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _listener.Dispose();
    }

    /// <summary>
    /// Returns true when any non-null sample is true, false when all samples are false,
    /// and null only when every sample is null.
    /// </summary>
    private sealed class AnyTrueConverter : StateListener<bool?>.IStateConverter
    {
        public bool? Convert(IEnumerable<bool?> states)
        {
            var any = false;
            foreach (var s in states)
            {
                if (s == null)
                    continue;

                any = true;
                if (s == true)
                    return true;
            }

            return any ? false : null;
        }
    }

    /// <summary>
    /// Equality on <see cref="Nullable{Boolean}"/> by value.
    /// </summary>
    private sealed class NullableBoolComparer : StateListener<bool?>.IStateComparer
    {
        public bool Equals(bool? x, bool? y) => x == y;
    }
}
