// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Notifications;

/// <summary>
/// Normalises an app id into a valid Windows AppUserModelID: no whitespace, at most 128 characters.
/// Only the Windows backend calls it; it lives outside Platforms/Windows so the rule is unit-tested
/// on every CI platform.
/// </summary>
internal static class NotificationAppId
{
    private const int MaxLength = 128;

    public static string Sanitize(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var chars = appId.Trim().Select(c => char.IsWhiteSpace(c) ? '.' : c).ToArray();
        var sanitized = new string(chars);
        return sanitized.Length > MaxLength ? sanitized[..MaxLength] : sanitized;
    }
}
