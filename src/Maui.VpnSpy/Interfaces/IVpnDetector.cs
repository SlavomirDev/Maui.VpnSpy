namespace Maui.VpnSpy;

/// <summary>
/// Platform abstraction over native VPN-state detection.
/// </summary>
public interface IVpnDetector
{
    /// <summary>
    /// Returns true when the device currently routes traffic through a VPN tunnel.
    /// Implementations are expected to be cheap and side-effect free so they can be polled.
    /// </summary>
    bool IsVpnActive();
}
