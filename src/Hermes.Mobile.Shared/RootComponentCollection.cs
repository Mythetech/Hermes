// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;

namespace Hermes.Mobile;

public sealed class RootComponentCollection
{
    private readonly List<(Type Type, string Selector)> _components = new();

    public void Add<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] TComponent>(string selector)
        where TComponent : IComponent
    {
        _components.Add((typeof(TComponent), selector));
    }

    internal IEnumerable<(Type Type, string Selector)> GetComponents() => _components;
}
