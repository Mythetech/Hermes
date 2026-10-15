// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.DataTransfer;

/// <summary>Tests that start drags run alone because only one drag can be active per process.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DragDropCollection
{
    public const string Name = "Drag and drop";
}
