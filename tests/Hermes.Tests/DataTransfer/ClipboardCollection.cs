// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.DataTransfer;

/// <summary>Tests that touch the static Clipboard run alone because they swap its process-wide backend.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ClipboardCollection
{
    public const string Name = "Clipboard";
}
