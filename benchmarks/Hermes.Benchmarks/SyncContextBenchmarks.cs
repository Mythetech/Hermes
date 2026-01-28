using BenchmarkDotNet.Attributes;
using Hermes.Benchmarks.Mocks;

namespace Hermes.Benchmarks;

/// <summary>
/// Benchmarks comparing Hermes direct interface calls vs Photino's reflection-based approach
/// for SynchronizationContext thread marshaling.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class SyncContextBenchmarks
{
    private MockPhotinoWindow _window = null!;
    private ReflectionBasedSyncContext _reflectionContext = null!;
    private DirectCallSyncContext _directContext = null!;

    [GlobalSetup]
    public void Setup()
    {
        _window = new MockPhotinoWindow();
        _reflectionContext = new ReflectionBasedSyncContext(_window);
        _directContext = new DirectCallSyncContext(_window);
    }

    /// <summary>
    /// Measures the overhead of Post() using reflection-based invoke (Photino approach).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Photino_ReflectionInvoke()
    {
        var counter = 0;
        _reflectionContext.Post(_ => counter++, null);
    }

    /// <summary>
    /// Measures the overhead of Post() using direct method call (Hermes approach).
    /// </summary>
    [Benchmark]
    public void Hermes_DirectInvoke()
    {
        var counter = 0;
        _directContext.Post(_ => counter++, null);
    }

    /// <summary>
    /// Measures bulk Post() operations with reflection (Photino approach).
    /// </summary>
    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public void Photino_BulkPost(int count)
    {
        var counter = 0;
        for (var i = 0; i < count; i++)
        {
            _reflectionContext.Post(_ => counter++, null);
        }
    }

    /// <summary>
    /// Measures bulk Post() operations with direct calls (Hermes approach).
    /// </summary>
    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public void Hermes_BulkPost(int count)
    {
        var counter = 0;
        for (var i = 0; i < count; i++)
        {
            _directContext.Post(_ => counter++, null);
        }
    }
}

/// <summary>
/// Benchmarks comparing the construction cost of sync contexts.
/// Photino uses reflection at construction time to cache MethodInfo.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class SyncContextConstructionBenchmarks
{
    private MockPhotinoWindow _window = null!;

    [GlobalSetup]
    public void Setup()
    {
        _window = new MockPhotinoWindow();
    }

    /// <summary>
    /// Measures construction overhead with reflection (Photino approach).
    /// GetField + GetMethod on every construction.
    /// </summary>
    [Benchmark(Baseline = true)]
    public ReflectionBasedSyncContext Photino_Construction()
    {
        return new ReflectionBasedSyncContext(_window);
    }

    /// <summary>
    /// Measures construction overhead with interface access (Hermes approach).
    /// In production, Hermes just stores the interface reference.
    /// </summary>
    [Benchmark]
    public DirectCallSyncContext Hermes_Construction()
    {
        return new DirectCallSyncContext(_window);
    }
}
