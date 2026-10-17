// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Hotkeys;

/// <summary>
/// Checks that an application id is a D-Bus well-known name, which desktop portals require.
/// </summary>
internal static class AppIdValidator
{
    public static bool IsValid(string? appId)
    {
        if (string.IsNullOrEmpty(appId) || appId.Length > 255)
            return false;

        var elements = appId.Split('.');
        if (elements.Length < 2)
            return false;

        foreach (var element in elements)
        {
            if (element.Length == 0 || char.IsAsciiDigit(element[0]))
                return false;

            foreach (var c in element)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))
                    return false;
            }
        }

        return true;
    }
}
