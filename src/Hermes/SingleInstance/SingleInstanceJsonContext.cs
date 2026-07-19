// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Hermes.SingleInstance;

[JsonSerializable(typeof(string[]))]
internal partial class SingleInstanceJsonContext : JsonSerializerContext
{
}
