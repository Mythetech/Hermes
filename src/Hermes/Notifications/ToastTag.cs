// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Security.Cryptography;
using System.Text;

namespace Hermes.Notifications;

/// <summary>
/// Derives the Windows toast tag from a notification id. WinRT caps tags at 64 characters, so longer ids
/// are replaced by their SHA-256 hex digest (exactly 64 characters). Kept platform-neutral so the rule is
/// unit-tested everywhere and the show and dismiss paths cannot diverge.
/// </summary>
internal static class ToastTag
{
    internal const int MaxLength = 64;

    public static string FromId(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return id.Length <= MaxLength
            ? id
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)));
    }
}
