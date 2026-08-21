// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;

namespace Hermes.Benchmarks;

/// <summary>
/// Benchmarks comparing Hermes vs Photino message pump implementations.
///
/// Photino: Unbounded channel, Thread.Sleep(200) on write failure, no batching
/// Hermes: Unbounded channel, single reader/writer, batches up to 16 messages
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class MessagePumpBenchmarks
{
    private PhotinoStyleMessagePump _photinoPump = null!;
    private HermesStyleMessagePump _hermesPump = null!;

    [GlobalSetup]
    public void Setup()
    {
        _photinoPump = new PhotinoStyleMessagePump();
        _hermesPump = new HermesStyleMessagePump();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _photinoPump.Dispose();
        _hermesPump.Dispose();
    }

    /// <summary>
    /// Single message send using Photino's approach.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Photino_SingleMessage()
    {
        _photinoPump.SendMessage("test message");
    }

    /// <summary>
    /// Single message send using Hermes's approach.
    /// </summary>
    [Benchmark]
    public void Hermes_SingleMessage()
    {
        _hermesPump.SendMessage("test message");
    }

    /// <summary>
    /// Burst of messages using Photino's approach.
    /// </summary>
    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public void Photino_BurstMessages(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _photinoPump.SendMessage($"message {i}");
        }
    }

    /// <summary>
    /// Burst of messages using Hermes's approach.
    /// </summary>
    [Benchmark]
    [Arguments(100)]
    [Arguments(1000)]
    public void Hermes_BurstMessages(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _hermesPump.SendMessage($"message {i}");
        }
    }
}

/// <summary>
/// Simulates Photino's message pump approach from PhotinoWebViewManager.
/// - Unbounded channel
/// - Thread.Sleep(200) when channel write fails
/// - No batching
/// </summary>
internal sealed class PhotinoStyleMessagePump : IDisposable
{
    private readonly Channel<string> _channel;
    private readonly Task _pumpTask;
    private readonly CancellationTokenSource _cts = new();
    private int _messagesProcessed;

    public PhotinoStyleMessagePump()
    {
        // Photino uses unbounded channel
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _pumpTask = RunPumpAsync();
    }

    public int MessagesProcessed => _messagesProcessed;

    /// <summary>
    /// Photino's SendMessage implementation (PhotinoWebViewManager.cs lines 91-94).
    /// </summary>
    public void SendMessage(string message)
    {
        // This is exactly Photino's code
        while (!_channel.Writer.TryWrite(message))
            Thread.Sleep(200);
    }

    private async Task RunPumpAsync()
    {
        try
        {
            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync(_cts.Token))
            {
                while (reader.TryRead(out var message))
                {
                    // Simulate processing (in real code this would call SendWebMessage)
                    Interlocked.Increment(ref _messagesProcessed);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _pumpTask.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }
}

/// <summary>
/// Simulates Hermes's message pump approach from HermesWebViewManager.
/// - Unbounded channel with single reader/writer
/// - Batches up to 16 messages per UI thread dispatch
/// </summary>
internal sealed class HermesStyleMessagePump : IDisposable
{
    private readonly Channel<string> _channel;
    private readonly Task _pumpTask;
    private readonly CancellationTokenSource _cts = new();
    private int _messagesProcessed;
    private int _batchesSent;

    public HermesStyleMessagePump()
    {
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,  // Optimized: SendMessage is called from single thread
            AllowSynchronousContinuations = false
        });

        _pumpTask = RunPumpAsync();
    }

    public int MessagesProcessed => _messagesProcessed;
    public int BatchesSent => _batchesSent;

    /// <summary>
    /// Hermes's SendMessage implementation from HermesWebViewManager.
    /// </summary>
    public void SendMessage(string message)
    {
        // Unbounded TryWrite only fails once the writer is completed during shutdown
        _channel.Writer.TryWrite(message);
    }

    private async Task RunPumpAsync()
    {
        const int BatchSize = 16;
        var batch = new string[BatchSize];

        try
        {
            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync(_cts.Token))
            {
                // Batch read for efficiency
                var count = 0;
                while (count < BatchSize && reader.TryRead(out var message))
                {
                    batch[count++] = message;
                }

                if (count > 0)
                {
                    // Simulate batched processing
                    Interlocked.Add(ref _messagesProcessed, count);
                    Interlocked.Increment(ref _batchesSent);

                    // Clear references
                    Array.Clear(batch, 0, count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        try { _pumpTask.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }
}
