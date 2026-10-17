// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Hotkeys;

/// <summary>
/// The outcome of registering a global hotkey.
/// </summary>
public enum HermesHotkeyStatus
{
    /// <summary>The hotkey is active.</summary>
    Registered,

    /// <summary>The id or the combination is already registered in this app, or another app owns the combination.</summary>
    Conflict,

    /// <summary>The user declined the shortcut.</summary>
    Denied,

    /// <summary>This platform or desktop cannot register it.</summary>
    Unsupported,

    /// <summary>The platform reported an error.</summary>
    Failed,

    /// <summary>The app already has the most hotkeys it can register.</summary>
    LimitExceeded,
}
