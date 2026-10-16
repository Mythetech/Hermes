// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Hotkeys;

/// <summary>
/// The result of registering a global hotkey.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="BoundAccelerator">The trigger in effect when registered; on Wayland, the desktop's description of the trigger the user approved.</param>
/// <param name="Reason">One sentence explaining an Unsupported, Denied, Failed, Conflict or LimitExceeded status.</param>
public sealed record HermesHotkeyResult(HermesHotkeyStatus Status, string? BoundAccelerator, string? Reason);
