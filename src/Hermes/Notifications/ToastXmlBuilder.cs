// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Security;
using System.Text;

namespace Hermes.Notifications;

/// <summary>
/// Builds the ToastGeneric XML consumed by Windows toast notifications. Kept platform-neutral so the
/// escaping rules are unit-tested on every CI platform.
/// </summary>
internal static class ToastXmlBuilder
{
    public static string Build(string id, string title, string? body, string? iconPath, bool silent)
    {
        var sb = new StringBuilder(256);
        sb.Append("<toast launch=\"").Append(SecurityElement.Escape(id)).Append("\">");
        sb.Append("<visual><binding template=\"ToastGeneric\">");
        sb.Append("<text>").Append(SecurityElement.Escape(title)).Append("</text>");
        if (!string.IsNullOrEmpty(body))
            sb.Append("<text>").Append(SecurityElement.Escape(body)).Append("</text>");
        if (!string.IsNullOrEmpty(iconPath))
            sb.Append("<image placement=\"appLogoOverride\" src=\"").Append(ToFileUri(iconPath)).Append("\"/>");
        sb.Append("</binding></visual>");
        if (silent)
            sb.Append("<audio silent=\"true\"/>");
        sb.Append("</toast>");
        return sb.ToString();
    }

    private static string ToFileUri(string path)
    {
        var normalized = path.Replace('\\', '/');
        var escaped = Uri.EscapeDataString(normalized).Replace("%2F", "/").Replace("%3A", ":");
        return "file:///" + escaped.TrimStart('/');
    }
}
