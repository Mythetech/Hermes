// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11, src/Components/WebView/WebView/src/Services/WebViewNavigationInterception.cs.
// Modifications Copyright (c) Mythetech, licensed under the MIT License. See THIRD-PARTY-NOTICES.md.

#nullable disable warnings

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Hermes.Blazor.WebView.Services;

internal sealed class WebViewNavigationInterception : INavigationInterception
{
    // On this platform, it's sufficient for the JS-side code to enable it unconditionally,
    // so there's no need to send a notification.
    public Task EnableNavigationInterceptionAsync() => Task.CompletedTask;
}
