using System.Diagnostics;
using System.Runtime.InteropServices;

using Foundation;

using NetworkExtension;

using ObjCRuntime;

namespace Maui.VpnSpy;

/// <summary>
/// iOS / Mac Catalyst implementation of <see cref="IVpnDetector"/>.
/// Combines NEVpnManager (app-managed VPN) with SCDynamicStore's PrimaryInterface (system VPN).
/// </summary>
internal sealed class VpnDetector : IVpnDetector, IDisposable
{
    private static readonly string[] _VpnInterfacePrefixes = ["utun", "tap", "tun", "ipsec", "ipsec2", "ppp"];

    private const string _IPv4StateKey = "State:/Network/Global/IPv4";
    private const string _IPv6StateKey = "State:/Network/Global/IPv6";
    private const string _PrimaryInterfaceKey = "PrimaryInterface";

    /// <summary>
    /// Set to <c>true</c> at runtime to dump every PrimaryInterface read into <see cref="Debug"/>.
    /// </summary>
    public static bool DiagnosticsEnabled = false;

    // Long-lived SCDynamicStoreRef. Created once and reused on every poll.
    private IntPtr _store;
    private bool _disposed;

    /// <summary>
    /// Creates the underlying SCDynamicStore handle. Failure is non-fatal — the store is just left at zero
    /// and <see cref="IsVpnActive"/> falls back to the NEVpnManager check only.
    /// </summary>
    public VpnDetector()
    {
        try
        {
            using var name = new NSString("Maui.VpnSpy.VpnDetector");
            _store = SCDynamicStoreCreate(IntPtr.Zero, name.Handle, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{nameof(VpnDetector)}] SCDynamicStoreCreate failed: {ex.Message}");
            _store = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Returns true when an active VPN tunnel is detected through either
    /// <see cref="NEVpnManager.SharedManager"/> or the system's default-route interface name.
    /// </summary>
    public bool IsVpnActive()
    {
        if (CheckAppManagedVpn())
            return true;

        return PrimaryInterfaceIsVpn();
    }

    /// <summary>
    /// Returns true when this app's own NEVpnManager configuration is in the Connected state.
    /// Catches per-app VPNs; misses every other VPN. Cheap, runs first.
    /// </summary>
    private static bool CheckAppManagedVpn()
    {
        try
        {
            var manager = NEVpnManager.SharedManager;
            return manager?.Connection?.Status == NEVpnStatus.Connected;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{nameof(VpnDetector)}] NEVpnManager check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reads the system default-route interface name from SCDynamicStore (IPv4 first, then IPv6) and
    /// returns true when its name has a VPN-style prefix. This filters out always-on system <c>utun</c>
    /// interfaces (iCloud Private Relay, AirDrop, Hotspot) because none of them register as the system
    /// PrimaryInterface unless the user has actually installed a tunnel-everything VPN.
    /// </summary>
    private bool PrimaryInterfaceIsVpn()
    {
        if (_store == IntPtr.Zero)
            return false;

        var primaryV4 = ReadPrimaryInterface(_IPv4StateKey);
        var primaryV6 = ReadPrimaryInterface(_IPv6StateKey);

        if (DiagnosticsEnabled)
            Debug.WriteLine($"[{nameof(VpnDetector)}] PrimaryInterface ipv4={primaryV4 ?? "<null>"} ipv6={primaryV6 ?? "<null>"}");

        return HasVpnPrefix(primaryV4) || HasVpnPrefix(primaryV6);
    }

    /// <summary>
    /// Reads <paramref name="dynamicStoreKey"/> from <see cref="_store"/> and returns the
    /// <c>PrimaryInterface</c> value within, or <c>null</c> when the key has no value.
    /// </summary>
    private string? ReadPrimaryInterface(string dynamicStoreKey)
    {
        var valueHandle = IntPtr.Zero;
        try
        {
            using var key = new NSString(dynamicStoreKey);
            valueHandle = SCDynamicStoreCopyValue(_store, key.Handle);
            if (valueHandle == IntPtr.Zero)
                return null;

            // Transfer ownership of the Copy* result to the NSDictionary wrapper.
            using var dict = Runtime.GetNSObject<NSDictionary>(valueHandle, owns: true);
            valueHandle = IntPtr.Zero;
            if (dict == null)
                return null;

            return dict[_PrimaryInterfaceKey]?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{nameof(VpnDetector)}] SCDynamicStoreCopyValue({dynamicStoreKey}) failed: {ex.Message}");
            return null;
        }
        finally
        {
            // Defensive release if GetNSObject was never reached.
            if (valueHandle != IntPtr.Zero)
                CFRelease(valueHandle);
        }
    }

    private static bool HasVpnPrefix(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        for (int i = 0; i < _VpnInterfacePrefixes.Length; i++)
        {
            if (name.StartsWith(_VpnInterfacePrefixes[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Releases the SCDynamicStore handle.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_store != IntPtr.Zero)
        {
            CFRelease(_store);
            _store = IntPtr.Zero;
        }
    }

    [DllImport("/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration", EntryPoint = "SCDynamicStoreCreate")]
    private static extern IntPtr SCDynamicStoreCreate(IntPtr allocator, IntPtr name, IntPtr callout, IntPtr context);

    [DllImport("/System/Library/Frameworks/SystemConfiguration.framework/SystemConfiguration", EntryPoint = "SCDynamicStoreCopyValue")]
    private static extern IntPtr SCDynamicStoreCopyValue(IntPtr store, IntPtr key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", EntryPoint = "CFRelease")]
    private static extern void CFRelease(IntPtr handle);
}
