// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Hermes.Blazor;
using Xunit;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// Razor generates @inject properties as private, and upstream's component factory
/// enumerates them with NonPublic binding flags, so a root component type must be rooted
/// with All. Anything weaker leaves injected properties null in trimmed and AOT builds
/// without any error.
/// </summary>
public class RootComponentAnnotationTests
{
    [Theory]
    [InlineData(typeof(RootComponentCollection))]
    [InlineData(typeof(HermesRootComponents))]
    public void EveryAddOverload_RootsAllMembersOfTheComponentType(Type collectionType)
    {
        var addMethods = collectionType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Add")
            .ToList();

        Assert.NotEmpty(addMethods);

        foreach (var method in addMethods)
        {
            var annotated = method.IsGenericMethodDefinition
                ? method.GetGenericArguments()[0].GetCustomAttributes<DynamicallyAccessedMembersAttribute>()
                : method.GetParameters()[0].GetCustomAttributes<DynamicallyAccessedMembersAttribute>();

            var attribute = Assert.Single(annotated);
            Assert.Equal(DynamicallyAccessedMemberTypes.All, attribute.MemberTypes);
        }
    }
}
