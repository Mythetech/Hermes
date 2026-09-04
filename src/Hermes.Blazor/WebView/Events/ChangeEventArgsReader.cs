// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// Forked by Mythetech from dotnet/aspnetcore tag v10.0.11, src/Components/Web/src/WebEventData/ChangeEventArgsReader.cs.
// Modifications Copyright (c) Mythetech, licensed under the MIT License. See THIRD-PARTY-NOTICES.md.

#nullable enable

using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Web;

namespace Hermes.Blazor.WebView.Events;

internal static class ChangeEventArgsReader
{
    private static readonly JsonEncodedText ValueKey = JsonEncodedText.Encode("value");

    internal static ChangeEventArgs Read(JsonElement jsonElement)
    {
        var changeArgs = new ChangeEventArgs();
        foreach (var property in jsonElement.EnumerateObject())
        {
            if (property.NameEquals(ValueKey.EncodedUtf8Bytes))
            {
                var value = property.Value;
                switch (value.ValueKind)
                {
                    case JsonValueKind.Null:
                        break;
                    case JsonValueKind.String:
                        changeArgs.Value = value.GetString();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        changeArgs.Value = value.GetBoolean();
                        break;
                    case JsonValueKind.Array:
                        changeArgs.Value = GetJsonElementStringArrayValue(value);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported {nameof(ChangeEventArgs)} value {jsonElement}.");
                }
                return changeArgs;
            }
            else
            {
                throw new JsonException($"Unknown property {property.Name}");
            }
        }

        return changeArgs;
    }

    private static string?[] GetJsonElementStringArrayValue(JsonElement jsonElement)
    {
        var result = new string?[jsonElement.GetArrayLength()];
        var elementIndex = 0;

        foreach (var arrayElement in jsonElement.EnumerateArray())
        {
            if (arrayElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Unsupported {nameof(JsonElement)} value kind '{arrayElement.ValueKind}' " +
                    $"(expected '{JsonValueKind.String}').");
            }

            result[elementIndex] = arrayElement.GetString();
            elementIndex++;
        }

        return result;
    }
}
