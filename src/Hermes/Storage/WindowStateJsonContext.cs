// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Hermes.Storage;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, WindowState>))]
internal partial class WindowStateJsonContext : JsonSerializerContext
{
}
