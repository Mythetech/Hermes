// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.DataTransfer;

/// <summary>
/// How a drag out of a window ended.
/// </summary>
public enum HermesDragResult
{
    /// <summary>A target accepted the data.</summary>
    Dropped,

    /// <summary>No target accepted it, the user cancelled, or the mouse button was already released.</summary>
    Canceled,
}
