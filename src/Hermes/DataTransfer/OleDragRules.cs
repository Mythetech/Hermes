// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

/// <summary>
/// The parts of a Windows OLE drag that need no Windows API: which button holds it and how it ended.
/// </summary>
internal static class OleDragRules
{
    public const int DragDropSDrop = 0x00040100;
    public const int DragDropSCancel = 0x00040101;
    public const uint DropEffectNone = 0;
    public const uint DropEffectCopy = 1;

    private const int VkLButton = 0x01;
    private const int VkRButton = 0x02;

    /// <summary>The virtual key of the primary button. GetAsyncKeyState reads physical buttons, so a swap moves it.</summary>
    public static int PrimaryButtonKey(bool buttonsSwapped) => buttonsSwapped ? VkRButton : VkLButton;

    /// <summary>True when a GetAsyncKeyState result has its high bit set, meaning the key is down now.</summary>
    public static bool IsDown(short asyncKeyState) => asyncKeyState < 0;

    /// <summary>
    /// Dropped only when a target took the copy, since DRAGDROP_S_DROP alone does not say that one did.
    /// </summary>
    public static HermesDragResult ToResult(int hresult, uint effect)
    {
        if (hresult < 0)
            throw new COMException("The native drag failed.", hresult);

        return hresult == DragDropSDrop && (effect & DropEffectCopy) != 0
            ? HermesDragResult.Dropped
            : HermesDragResult.Canceled;
    }
}
