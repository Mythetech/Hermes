// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Hotkeys;

/// <summary>Tests that touch the static GlobalHotkeys run alone because they swap its process-wide backend.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalHotkeysCollection
{
    public const string Name = "Global hotkeys";
}
