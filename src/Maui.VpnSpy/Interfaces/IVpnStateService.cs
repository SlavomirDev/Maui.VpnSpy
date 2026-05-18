namespace Maui.VpnSpy;

/// <summary>
/// Tracks whether the device is currently routing traffic through a VPN.
/// Powered by an <see cref="IVpnDetector"/> sample collected on a polling loop.
/// </summary>
public interface IVpnStateService : IDisposable
{
    /// <summary>
    /// True when the most recent sample reported an active VPN, false when off,
    /// null until the first sample is available.
    /// </summary>
    bool? IsVpnActive { get; }

    /// <summary>
    /// Raised whenever <see cref="IsVpnActive"/> changes. Args: (oldValue, newValue).
    /// </summary>
    event Action<bool?, bool?> OnChanged;

    /// <summary>
    /// Forces an immediate re-evaluation of the underlying detector.
    /// </summary>
    void Refresh();
}
