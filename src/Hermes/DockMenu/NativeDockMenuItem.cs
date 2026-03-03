// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.DockMenu;

/// <summary>
/// Represents a dock menu item with properties that update the native backend immediately.
/// </summary>
public sealed class NativeDockMenuItem
{
    private readonly IDockMenuBackend _backend;
    private string _label;
    private bool _isEnabled = true;
    private bool _isChecked;

    internal NativeDockMenuItem(IDockMenuBackend backend, string itemId, string label)
    {
        _backend = backend;
        Id = itemId;
        _label = label;
    }

    /// <summary>
    /// The unique identifier for this menu item.
    /// </summary>
    public string Id { get; }

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
            _backend.SetItemLabel(Id, value);
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
            _backend.SetItemEnabled(Id, value);
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
            _backend.SetItemChecked(Id, value);
        }
    }

    #region Builder Methods

    /// <summary>
    /// Set the initial enabled state (builder pattern for use during creation).
    /// </summary>
    /// <param name="enabled">Whether the item should be enabled.</param>
    /// <returns>This item for method chaining.</returns>
    public NativeDockMenuItem WithEnabled(bool enabled)
    {
        _isEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Set the initial checked state (builder pattern for use during creation).
    /// </summary>
    /// <param name="isChecked">Whether the item should be checked.</param>
    /// <returns>This item for method chaining.</returns>
    public NativeDockMenuItem WithChecked(bool isChecked)
    {
        _isChecked = isChecked;
        return this;
    }

    #endregion
}
