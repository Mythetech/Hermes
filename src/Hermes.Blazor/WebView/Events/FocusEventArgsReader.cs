// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11, src/Components/Web/src/WebEventData/FocusEventArgsReader.cs.
// Modifications Copyright (c) Mythetech, licensed under the MIT License. See THIRD-PARTY-NOTICES.md.

#nullable enable

using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;

namespace Hermes.Blazor.WebView.Events;

internal static class FocusEventArgsReader
{
    private static readonly JsonEncodedText Type = JsonEncodedText.Encode("type");

    internal static FocusEventArgs Read(JsonElement jsonElement)
    {
        var eventArgs = new FocusEventArgs();
        foreach (var property in jsonElement.EnumerateObject())
        {
            if (property.NameEquals(Type.EncodedUtf8Bytes))
            {
                eventArgs.Type = property.Value.GetString()!;
            }
            else
            {
                throw new JsonException($"Unknown property {property.Name}");
            }
        }
        return eventArgs;
    }
}
