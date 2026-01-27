namespace Hermes.Menu;

/// <summary>
/// Represents a menu item with properties that update the native backend immediately.
/// </summary>
public sealed class NativeMenuItem
{
    private readonly Action<string, bool>? _setEnabled;
    private readonly Action<string, bool>? _setChecked;
    private readonly Action<string, string>? _setLabel;
    private readonly Action<string, string>? _setAccelerator;

    private string _label;
    private Accelerator? _accelerator;
    private bool _isEnabled = true;
    private bool _isChecked;

    /// <summary>
    /// Creates a menu item for a menu bar menu.
    /// </summary>
    internal NativeMenuItem(
        Abstractions.IMenuBackend backend,
        string menuLabel,
        string itemId,
        string label,
        Accelerator? accelerator = null)
    {
        Id = itemId;
        MenuLabel = menuLabel;
        _label = label;
        _accelerator = accelerator;

        // Capture backend operations as delegates
        _setEnabled = (id, enabled) => backend.SetItemEnabled(menuLabel, id, enabled);
        _setChecked = (id, isChecked) => backend.SetItemChecked(menuLabel, id, isChecked);
        _setLabel = (id, newLabel) => backend.SetItemLabel(menuLabel, id, newLabel);
        _setAccelerator = (id, accel) => backend.SetItemAccelerator(menuLabel, id, accel);
    }

    /// <summary>
    /// Creates a menu item for a context menu.
    /// </summary>
    internal NativeMenuItem(
        Abstractions.IContextMenuBackend backend,
        string itemId,
        string label,
        Accelerator? accelerator = null)
    {
        Id = itemId;
        MenuLabel = string.Empty;
        _label = label;
        _accelerator = accelerator;

        // Capture backend operations as delegates
        _setEnabled = (id, enabled) => backend.SetItemEnabled(id, enabled);
        _setChecked = (id, isChecked) => backend.SetItemChecked(id, isChecked);
        _setLabel = (id, newLabel) => backend.SetItemLabel(id, newLabel);
        _setAccelerator = null; // Context menus typically don't update accelerators
    }

    /// <summary>
    /// The unique identifier for this menu item.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The label of the parent menu containing this item.
    /// Empty for context menu items.
    /// </summary>
    public string MenuLabel { get; }

    /// <summary>
    /// Get or set the display label. Updates the native menu immediately.
    /// </summary>
    public string Label
    {
        get => _label;
        set
        {
            if (_label == value) return;
            _label = value;
            _setLabel?.Invoke(Id, value);
        }
    }

    /// <summary>
    /// Get or set whether the item is enabled. Updates the native menu immediately.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            _setEnabled?.Invoke(Id, value);
        }
    }

    /// <summary>
    /// Get or set the checked state. Updates the native menu immediately.
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            _setChecked?.Invoke(Id, value);
        }
    }

    /// <summary>
    /// Get or set the keyboard accelerator. Updates the native menu immediately.
    /// </summary>
    public Accelerator? Accelerator
    {
        get => _accelerator;
        set
        {
            if (_accelerator == value) return;
            _accelerator = value;
            _setAccelerator?.Invoke(Id, value?.ToPlatformString() ?? string.Empty);
        }
    }

    #region Builder Methods

    /// <summary>
    /// Set the keyboard accelerator (builder pattern for use during creation).
    /// </summary>
    /// <param name="accelerator">Accelerator string like "Ctrl+S" or "Cmd+Shift+N".</param>
    /// <returns>This item for method chaining.</returns>
    public NativeMenuItem WithAccelerator(string accelerator)
    {
        _accelerator = Menu.Accelerator.Parse(accelerator);
        return this;
    }

    /// <summary>
    /// Set the initial enabled state (builder pattern for use during creation).
    /// </summary>
    /// <param name="enabled">Whether the item should be enabled.</param>
    /// <returns>This item for method chaining.</returns>
    public NativeMenuItem WithEnabled(bool enabled)
    {
        _isEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Set the initial checked state (builder pattern for use during creation).
    /// </summary>
    /// <param name="isChecked">Whether the item should be checked.</param>
    /// <returns>This item for method chaining.</returns>
    public NativeMenuItem WithChecked(bool isChecked)
    {
        _isChecked = isChecked;
        return this;
    }

    #endregion
}
