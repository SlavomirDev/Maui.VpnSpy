using Android.Content;
using Android.Net;
using Android.OS;

using Debug = System.Diagnostics.Debug;

namespace Maui.VpnSpy;

/// <summary>
/// Android implementation of <see cref="IVpnDetector"/>.
/// Backed by <see cref="ConnectivityManager"/>'s <c>TRANSPORT_VPN</c> capability.
/// </summary>
internal sealed class VpnDetector : IVpnDetector
{
    /// <summary>
    /// Returns true when at least one currently bound network reports the VPN transport.
    /// Falls back to scanning all known networks because some Android versions do not put
    /// the VPN on the active network slot. Avoids deprecated APIs on Android S (API 31) and later.
    /// </summary>
    public bool IsVpnActive()
    {
        try
        {
            var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
            if (context.GetSystemService(Context.ConnectivityService) is not ConnectivityManager cm)
                return false;

            var active = cm.ActiveNetwork;
            if (active != null && cm.GetNetworkCapabilities(active) is { } activeCaps && activeCaps.HasTransport(TransportType.Vpn))
                return true;

            // GetAllNetworks is deprecated starting API 31 (Android S).
            // On newer Android versions try checking the process-bound network as a fallback.
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                var bound = cm.BoundNetworkForProcess;
                if (bound != null && cm.GetNetworkCapabilities(bound) is { } boundCaps && boundCaps.HasTransport(TransportType.Vpn))
                    return true;

                return false;
            }

#pragma warning disable CA1422
            var allNetworks = cm.GetAllNetworks();
            if (allNetworks == null)
                return false;
#pragma warning restore CA1422

            foreach (var network in allNetworks)
            {
                if (network == null)
                    continue;

                if (cm.GetNetworkCapabilities(network) is { } caps && caps.HasTransport(TransportType.Vpn))
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{nameof(VpnDetector)}] VPN check failed: {ex.Message}");
            return false;
        }
    }
}
