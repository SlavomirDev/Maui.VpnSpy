# Maui.VpnSpy

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Cross-platform VPN-state detection for **.NET MAUI**. Lightweight detector + polling service with change events, ready for DI. Targets `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`.

---

## Why

VPN detection on mobile is platform-specific, error-prone and full of edge cases (deprecated APIs on Android S+, iCloud Private Relay on iOS, system `utun*` interfaces, app-managed VPNs, etc.). `Maui.VpnSpy` wraps all of it behind a single `IVpnDetector` abstraction plus a polling `IVpnStateService` that raises events on state changes.

## Features

- **`IVpnDetector`** — synchronous, side-effect-free check of "is VPN currently active?"
- **`IVpnStateService`** — polls the detector on a background timer, exposes the latest state, raises `OnChanged(old, new)` events, and remembers whether VPN was ever seen in the current session
- **`AddVpnSpy()`** — one-call DI registration
- Pure .NET MAUI; no extra dependencies beyond `Microsoft.Maui.Controls`

## Platform support

| Platform | Status | Backed by |
| --- | --- | --- |
| Android (API 24+) | Yes | `ConnectivityManager` + `TRANSPORT_VPN` (uses non-deprecated paths on API 31+) |
| iOS (12.2+) | Yes | `NEVpnManager` + SCDynamicStore `PrimaryInterface` |
| Mac Catalyst (15+) | Yes | Same as iOS |
| Windows | Not supported in this release | — |

## Install

```shell
dotnet add package MirDev.Maui.VpnSpy
```

Or via the Package Manager Console:

```powershell
Install-Package MirDev.Maui.VpnSpy
```

## Quick start

Register the services in your `MauiProgram.cs`:

```csharp
using Maui.VpnSpy;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .AddVpnSpy(); // registers IVpnDetector + IVpnStateService

        return builder.Build();
    }
}
```

Inject and use anywhere:

```csharp
using Maui.VpnSpy;

public sealed class HomeViewModel
{
    public HomeViewModel(IVpnStateService vpn)
    {
        vpn.OnChanged += (oldValue, newValue) =>
        {
            if (newValue == true)
            {
                // VPN is active right now
            }
        };

        // Force an immediate check (otherwise the next scheduled poll will fire shortly).
        vpn.Refresh();
    }
}
```

Need a one-shot synchronous check? Inject `IVpnDetector` directly:

```csharp
public sealed class TestSession
{
    private readonly IVpnDetector _detector;

    public TestSession(IVpnDetector detector) => _detector = detector;

    public bool IsCheating() => _detector.IsVpnActive();
}
```

## API

### `IVpnDetector`

| Member | Description |
| --- | --- |
| `bool IsVpnActive()` | Returns `true` when the device is currently routing traffic through a VPN tunnel. |

### `IVpnStateService : IDisposable`

| Member | Description |
| --- | --- |
| `bool? IsVpnActive` | Latest sample. `null` until the first poll completes. |
| `event Action<bool?, bool?> OnChanged` | Fires when `IsVpnActive` changes. Args: `(oldValue, newValue)`. |
| `void Refresh()` | Forces an immediate poll of the underlying detector. |

The polling interval is currently fixed at 4 seconds.

### `VpnSpyExtensions`

| Method | Description |
| --- | --- |
| `AddVpnDetector()` | Registers only `IVpnDetector` as a singleton. |
| `AddVpnStateService()` | Registers `IVpnStateService` and pulls in `IVpnDetector`. |
| `AddVpnSpy()` | Convenience alias for `AddVpnStateService()`. |

## How it works

- **Android.** The detector first asks `ConnectivityManager` for the active network and checks for `TRANSPORT_VPN`. On API 31+ it falls back to the process-bound network (because `GetAllNetworks` is deprecated). On older versions it sweeps every known network.
- **iOS / Mac Catalyst.** First a cheap `NEVpnManager.SharedManager.Connection.Status` check catches per-app VPNs. If that comes back empty, the detector reads `State:/Network/Global/IPv4` (and IPv6) from `SCDynamicStore` and checks whether the system's `PrimaryInterface` name has a VPN-style prefix (`utun`, `tun`, `tap`, `ipsec`, `ppp`). This filters out the always-on `utun` interfaces used by iCloud Private Relay and AirDrop, since they never register as the primary interface unless an actual tunnel-everything VPN is configured.

## License

[MIT](LICENSE) © 2026 SlavomirTheDev
