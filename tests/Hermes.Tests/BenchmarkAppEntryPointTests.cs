// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Hermes.Tests;

/// <summary>
/// Guards every desktop app entry point in the repo (samples and benchmark apps)
/// against losing its [STAThread] main thread. Windows requires STA for WebView2;
/// without the attribute every Windows run fails at window creation. Two silent
/// ways to lose it: top-level statements (the synthesized entry point cannot carry
/// the attribute) and async Main (the attribute sits on the user method but the
/// real entry point is the compiler-generated wrapper, which does not inherit it).
/// </summary>
public sealed class BenchmarkAppEntryPointTests
{
    public static TheoryData<string> DesktopAppEntryPoints()
    {
        var root = FindRepositoryRoot();
        var data = new TheoryData<string>();

        foreach (var parent in new[] { "samples", Path.Combine("benchmarks", "Hermes.Benchmarks.Apps") })
        {
            foreach (var directory in Directory.GetDirectories(Path.Combine(root, parent)))
            {
                if (File.Exists(Path.Combine(directory, "Program.cs")))
                    data.Add(Path.Combine(parent, Path.GetFileName(directory), "Program.cs"));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DesktopAppEntryPoints))]
    public void DesktopApp_EntryPoint_IsSynchronousWithStaThread(string relativePath)
    {
        var programPath = Path.Combine(FindRepositoryRoot(), relativePath);
        var source = File.ReadAllText(programPath);

        Assert.True(source.Contains("[STAThread]"),
            $"{relativePath} has no [STAThread] attribute, so its entry point starts MTA and " +
            "the app fails the STA check at window creation on Windows. Top-level statements " +
            "cannot carry the attribute; use an explicit [STAThread] static void Main.");

        Assert.True(System.Text.RegularExpressions.Regex.IsMatch(source, @"static\s+(void|int)\s+Main\s*\("),
            $"{relativePath} has no explicit synchronous Main. [STAThread] on an async Main is " +
            "silently ignored: the real entry point is the compiler-generated wrapper, which " +
            "does not inherit the attribute. Use a synchronous [STAThread] Main and block on " +
            "trailing async work with GetAwaiter().GetResult().");

        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(source, @"async\s+(Task|ValueTask)[^\S\n]*(<[^>]+>)?\s+Main"),
            $"{relativePath} declares an async Main, which starts the process MTA even with " +
            "[STAThread] present. Use a synchronous [STAThread] Main.");
    }

    /// <summary>
    /// The authoritative check: reads the compiled assembly's real entry point
    /// from PE metadata and asserts [STAThread] is on it. This is the method the
    /// Windows runtime reads the apartment from, so it catches every variant the
    /// source checks above cannot prove: async Main (attribute lands on the user
    /// method, entry point is the synthesized wrapper without it), top-level
    /// statements, or the attribute on the wrong method.
    /// </summary>
    [Theory]
    [InlineData("HermesTestApp")]
    [InlineData("PhotinoTestApp")]
    [InlineData("PhotinoXTestApp")]
    public void BenchmarkApp_CompiledEntryPoint_HasStaThreadAttribute(string appName)
    {
        var configuration = typeof(BenchmarkAppEntryPointTests).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

        var assemblyPath = Path.Combine(
            FindRepositoryRoot(), "benchmarks", "Hermes.Benchmarks.Apps", appName,
            "bin", configuration, "net11.0", $"{appName}.dll");

        Assert.True(File.Exists(assemblyPath),
            $"Expected compiled benchmark app at {assemblyPath}. The test project references " +
            "the benchmark apps with ReferenceOutputAssembly=false so building the tests " +
            "builds them; if this fires, that wiring broke.");

        Assert.True(CompiledEntryPointHasStaThread(assemblyPath),
            $"{appName}'s compiled entry point has no [STAThread] attribute, so the main " +
            "thread starts MTA and every Windows run fails at window creation. If Program.cs " +
            "looks correct, check for an async Main: the attribute is not inherited by the " +
            "compiler-generated entry point wrapper. Use a synchronous [STAThread] Main.");
    }

    private static bool CompiledEntryPointHasStaThread(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);

        var corHeader = peReader.PEHeaders.CorHeader
            ?? throw new InvalidOperationException($"{assemblyPath} is not a managed assembly");

        var entryPointToken = corHeader.EntryPointTokenOrRelativeVirtualAddress;
        Assert.True(entryPointToken != 0, $"{assemblyPath} has no managed entry point token");

        var metadata = peReader.GetMetadataReader();
        var entryPoint = metadata.GetMethodDefinition(
            (MethodDefinitionHandle)MetadataTokens.EntityHandle(entryPointToken));

        foreach (var attributeHandle in entryPoint.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(attributeHandle);
            if (GetAttributeTypeName(metadata, attribute) == "System.STAThreadAttribute")
                return true;
        }

        return false;
    }

    private static string? GetAttributeTypeName(MetadataReader metadata, CustomAttribute attribute)
    {
        EntityHandle typeHandle;
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                typeHandle = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
                break;
            case HandleKind.MethodDefinition:
                typeHandle = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
                break;
            default:
                return null;
        }

        switch (typeHandle.Kind)
        {
            case HandleKind.TypeReference:
                var reference = metadata.GetTypeReference((TypeReferenceHandle)typeHandle);
                return FullName(metadata.GetString(reference.Namespace), metadata.GetString(reference.Name));
            case HandleKind.TypeDefinition:
                var definition = metadata.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                return FullName(metadata.GetString(definition.Namespace), metadata.GetString(definition.Name));
            default:
                return null;
        }

        static string FullName(string ns, string name)
            => ns.Length == 0 ? name : $"{ns}.{name}";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hermes.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root from the test base directory");
    }
}
