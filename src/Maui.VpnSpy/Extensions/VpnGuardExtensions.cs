using Maui.VpnSpy;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maui.VpnSpy;

/// <summary>
/// DI registration helpers for the Maui.VpnSpy library.
/// </summary>
public static class VpnSpyExtensions
{
    /// <summary>
    /// Registers <see cref="IVpnDetector"/> with its platform-specific implementation
    /// (Android / iOS / Mac Catalyst) as a singleton. Safe to call multiple times.
    /// </summary>
    public static MauiAppBuilder AddVpnDetector(this MauiAppBuilder builder)
    {
#if ANDROID || IOS || MACCATALYST
        builder.Services.TryAddSingleton<IVpnDetector, VpnDetector>();
#endif
        return builder;
    }

    /// <summary>
    /// Registers <see cref="IVpnStateService"/> as a singleton plus its <see cref="IVpnDetector"/> dependency.
    /// Safe to call multiple times.
    /// </summary>
    public static MauiAppBuilder AddVpnStateService(this MauiAppBuilder builder)
    {
        builder.AddVpnDetector();
#if ANDROID || IOS || MACCATALYST
        builder.Services.TryAddSingleton<IVpnStateService, VpnStateService>();
#endif
        return builder;
    }

    /// <summary>
    /// Registers the full Maui.VpnSpy stack: <see cref="IVpnDetector"/> and <see cref="IVpnStateService"/>.
    /// Recommended entry point for app startup.
    /// </summary>
    public static MauiAppBuilder AddVpnSpy(this MauiAppBuilder builder)
        => builder.AddVpnStateService();
}
