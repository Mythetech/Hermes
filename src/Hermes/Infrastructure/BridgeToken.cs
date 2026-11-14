// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Security.Cryptography;

namespace Hermes.Infrastructure;

/// <summary>
/// The per-window secret the Linux bridge prefixes to every message, so the native layer can tell the top frame's bridge from other script.
/// </summary>
internal static class BridgeToken
{
    public const int Length = 32;

    public static string Create() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(Length / 2));
}
