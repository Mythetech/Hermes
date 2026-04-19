// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.Content;
using Hermes.Contracts.Plugins;

namespace Hermes.Mobile.Android.Plugins;

public sealed class AndroidClipboard : IClipboard
{
    private readonly ClipboardManager _clipboard;

    public AndroidClipboard(Context context)
    {
        _clipboard = (ClipboardManager)context.GetSystemService(Context.ClipboardService)!;
    }

    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var clip = ClipData.NewPlainText("Hermes", text);
        _clipboard.PrimaryClip = clip;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken ct = default)
    {
        var clip = _clipboard.PrimaryClip;
        if (clip is null || clip.ItemCount == 0)
            return Task.FromResult<string?>(null);

        var text = clip.GetItemAt(0)?.Text;
        return Task.FromResult<string?>(text);
    }
}
