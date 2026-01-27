namespace Hermes.Tests;

public class MenuTests
{
    [Fact]
    public void MenuItem_ShouldStoreProperties()
    {
        // Arrange & Act
        var item = new MenuItem("File", "file");

        // Assert
        Assert.Equal("File", item.Label);
        Assert.Equal("file", item.Id);
    }

    [Fact]
    public void MenuItem_ShouldSupportChildren()
    {
        // Arrange
        var parent = new MenuItem("File", "file");
        var child = new MenuItem("Open", "open");

        // Act
        parent.Children.Add(child);

        // Assert
        Assert.Single(parent.Children);
        Assert.Equal("Open", parent.Children[0].Label);
    }

    [Fact]
    public void MenuItem_ShouldSupportAccelerator()
    {
        // Arrange & Act
        var item = new MenuItem("Save", "save", accelerator: "Ctrl+S");

        // Assert
        Assert.Equal("Ctrl+S", item.Accelerator);
    }
}
