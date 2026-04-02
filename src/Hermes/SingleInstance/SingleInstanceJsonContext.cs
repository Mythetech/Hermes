// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Text.Json.Serialization;

namespace Hermes.SingleInstance;

[JsonSerializable(typeof(string[]))]
internal partial class SingleInstanceJsonContext : JsonSerializerContext
{
}
