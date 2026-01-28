namespace Hermes.Menu;

/// <summary>
/// Position hints for app menu item insertion.
/// </summary>
public static class AppMenuPosition
{
    /// <summary>
    /// Insert before the Quit item (default position).
    /// </summary>
    public const string BeforeQuit = "before-quit";

    /// <summary>
    /// Insert after the About item.
    /// </summary>
    public const string AfterAbout = "after-about";

    /// <summary>
    /// Insert at the top of the menu.
    /// </summary>
    public const string Top = "top";
}
