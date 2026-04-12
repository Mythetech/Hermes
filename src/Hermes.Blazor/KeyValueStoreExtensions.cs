// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Blazor;

/// <summary>
/// Extension methods for registering the Hermes key-value store with a Blazor application.
/// </summary>
public static class KeyValueStoreExtensions
{
    /// <summary>
    /// Registers <see cref="IHermesKeyValueStore"/> as a singleton resolving to
    /// <see cref="HermesStore.Default"/>. Components can then inject the store via
    /// <c>[Inject] IHermesKeyValueStore Store</c>.
    /// </summary>
    /// <param name="builder">The Blazor app builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// Apps that need a non-default store can call <see cref="HermesStore.Open(string)"/>
    /// directly, or call <see cref="AddKeyValueStore(HermesBlazorAppBuilder, string)"/>
    /// to bind the injected store to a named store instead of the default.
    /// </remarks>
    public static HermesBlazorAppBuilder AddKeyValueStore(this HermesBlazorAppBuilder builder)
    {
        builder.Services.AddSingleton<IHermesKeyValueStore>(_ => HermesStore.Default);
        return builder;
    }

    /// <summary>
    /// Registers <see cref="IHermesKeyValueStore"/> as a singleton resolving to
    /// the named store via <see cref="HermesStore.Open(string)"/>.
    /// </summary>
    /// <param name="builder">The Blazor app builder.</param>
    /// <param name="storeName">The store name to bind for injection.</param>
    /// <returns>The builder for chaining.</returns>
    public static HermesBlazorAppBuilder AddKeyValueStore(
        this HermesBlazorAppBuilder builder,
        string storeName)
    {
        builder.Services.AddSingleton<IHermesKeyValueStore>(_ => HermesStore.Open(storeName));
        return builder;
    }
}
