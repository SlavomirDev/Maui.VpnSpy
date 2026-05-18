namespace Maui.VpnSpy;

/// <summary>
/// Periodically polls a set of state-processor delegates, folds the results through a converter
/// and raises events whenever the merged state changes. Used internally by <see cref="VpnStateService"/>
/// to keep VPN-state sampling self-contained.
/// </summary>
internal sealed class StateListener<TState> : IDisposable
{
    private readonly object _processorsLock = new();
    private readonly List<StateProcessor> _processors = new();
    private readonly IStateComparer _comparer;
    private readonly IStateConverter _converter;
    private readonly Timer _timer;
    private TState? _currentState;
    private bool _disposed;

    /// <summary>
    /// Latest converted state. Updated only when the comparer reports a change.
    /// </summary>
    public TState? CurrentState
    {
        get => _currentState;
        private set
        {
            if (!_comparer.Equals(_currentState, value))
            {
                var oldState = _currentState;
                var newState = value;

                OnStateChanging?.Invoke(oldState, newState);
                _currentState = newState;
                OnStateChanged?.Invoke(oldState, newState);
            }
        }
    }

    /// <summary>
    /// Raised right before <see cref="CurrentState"/> is updated.
    /// </summary>
    public event Action<TState?, TState?>? OnStateChanging;

    /// <summary>
    /// Raised right after <see cref="CurrentState"/> has been updated.
    /// </summary>
    public event Action<TState?, TState?>? OnStateChanged;

    /// <summary>
    /// Polling period in milliseconds. Applied on the next <see cref="Start"/> call.
    /// </summary>
    public int CheckIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Creates a listener with the given converter and an optional comparer.
    /// </summary>
    public StateListener(IStateConverter converter, IStateComparer? comparer = null, bool autoStart = true)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _comparer = comparer ?? new DefaultStateComparer();

        _timer = new Timer(CheckStates, null, Timeout.Infinite, Timeout.Infinite);

        if (autoStart)
            Start();
    }

    /// <summary>
    /// Adds a processor whose result will be folded into the merged state on each tick.
    /// </summary>
    public void AddProcessor(StateProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        lock (_processorsLock)
            _processors.Add(processor);
    }

    /// <summary>
    /// Starts (or restarts) the periodic check loop.
    /// </summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Change(0, CheckIntervalMs);
    }

    /// <summary>
    /// Stops the periodic check loop. Safe to call after dispose.
    /// </summary>
    public void Stop()
    {
        if (_disposed)
            return;

        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Forces a one-off run of all processors and updates <see cref="CurrentState"/>.
    /// </summary>
    public Task<TState?> CheckNow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return UpdateStateAsync();
    }

    private void CheckStates(object? _)
    {
        try
        {
            _ = UpdateStateAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{nameof(StateListener<TState>)}] tick failed: {ex.Message}");
        }
    }

    private async Task<TState?> UpdateStateAsync()
    {
        StateProcessor[] snapshot;
        lock (_processorsLock)
            snapshot = _processors.ToArray();

        if (snapshot.Length == 0)
            return CurrentState;

        var results = new List<TState?>(snapshot.Length);
        foreach (var processor in snapshot)
        {
            try
            {
                var state = await processor().ConfigureAwait(false);
                results.Add(state);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[{nameof(StateListener<TState>)}] processor failed: {ex.Message}");
                results.Add(default);
            }
        }

        try
        {
            var newState = _converter.Convert(results);
            CurrentState = newState;
            return newState;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[{nameof(StateListener<TState>)}] converter failed: {ex.Message}");
            return CurrentState;
        }
    }

    /// <summary>
    /// Disposes the underlying timer and prevents further state changes.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try { _timer.Change(Timeout.Infinite, Timeout.Infinite); } catch { /* swallow */ }
        _timer.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Async delegate that produces a single sample of the tracked state.
    /// </summary>
    public delegate Task<TState?> StateProcessor();

    /// <summary>
    /// Compares two state values for equality.
    /// </summary>
    public interface IStateComparer
    {
        /// <summary>
        /// Returns true when the two states should be considered equivalent.
        /// </summary>
        bool Equals(TState? x, TState? y);
    }

    /// <summary>
    /// Default comparer that delegates to <see cref="EqualityComparer{T}.Default"/>.
    /// </summary>
    public sealed class DefaultStateComparer : IStateComparer
    {
        /// <summary>
        /// Compares values via the framework's default equality comparer.
        /// </summary>
        public bool Equals(TState? x, TState? y) => EqualityComparer<TState?>.Default.Equals(x, y);
    }

    /// <summary>
    /// Strategy that folds multiple per-processor states into a single state.
    /// </summary>
    public interface IStateConverter
    {
        /// <summary>
        /// Folds the per-processor states into a single state value.
        /// </summary>
        TState? Convert(IEnumerable<TState?> states);
    }
}
