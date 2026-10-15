// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;

namespace Hermes.Blazor;

/// <summary>
/// Blazor events Hermes adds. <c>@onhermesdrop</c> fires on the element another app's data was dropped on,
/// in windows with drag and drop enabled.
/// </summary>
// Razor only discovers [EventHandler] attributes on a class named EventHandlers.
[EventHandler("onhermesdrop", typeof(HermesDropEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}
