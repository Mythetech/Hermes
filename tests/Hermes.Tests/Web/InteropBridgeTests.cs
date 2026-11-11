// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hermes.Testing;
using Hermes.Web.Interop;
using Xunit;

namespace Hermes.Tests.Web;

public sealed class InteropBridgeTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly List<InteropBridge> _bridges = [];

    public void Dispose()
    {
        foreach (var bridge in _bridges)
            bridge.Stop();
    }

    [Fact]
    public async Task Invoke_ReturnsARecordAsCamelCaseJson()
    {
        var (_, backend) = Start(b => b.Handle("person", () => new Person("Ada", 36)));

        backend.SimulateWebMessage(Invoke("1", "person"));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("result", reply.GetProperty("type").GetString());
        Assert.Equal("1", reply.GetProperty("id").GetString());
        Assert.Equal("""{"name":"Ada","age":36}""", reply.GetProperty("value").GetRawText());
    }

    [Fact]
    public async Task Invoke_ReturnsArraysAndDictionaries()
    {
        var (_, backend) = Start(b =>
        {
            b.Handle("list", () => new[] { 1, 2 });
            b.Handle("map", () => new Dictionary<string, int> { ["a"] = 1 });
        });

        backend.SimulateWebMessage(Invoke("1", "list"));
        await SentAsync(backend, 1);
        backend.SimulateWebMessage(Invoke("2", "map"));
        var replies = await SentAsync(backend, 2);

        Assert.Equal("[1,2]", replies[0].GetProperty("value").GetRawText());
        Assert.Equal("""{"a":1}""", replies[1].GetProperty("value").GetRawText());
    }

    [Fact]
    public async Task Invoke_PassesFourArgumentsInOrder()
    {
        var (_, backend) = Start(b => b.Handle(
            "four",
            (int a, string text, bool flag, double d) => $"{a}|{text}|{flag}|{d.ToString(CultureInfo.InvariantCulture)}"));

        backend.SimulateWebMessage(Invoke("1", "four", """[1,"x",true,2.5]"""));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("1|x|True|2.5", reply.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Invoke_DeserializesARecordArgument()
    {
        var (_, backend) = Start(b => b.Handle("describe", (Person p) => $"{p.Name} is {p.Age}"));

        backend.SimulateWebMessage(Invoke("1", "describe", """[{"name":"Ada","age":36}]"""));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("Ada is 36", reply.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Invoke_WithTheWrongArgumentCount_RepliesWithAnError()
    {
        var (_, backend) = Start(b => b.Handle("echo", (string s) => s));

        backend.SimulateWebMessage(Invoke("1", "echo"));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("error", reply.GetProperty("type").GetString());
        Assert.Equal("'echo' takes 1 argument(s) but was called with 0", reply.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invoke_UnknownMethod_RepliesWithAnError()
    {
        var (_, backend) = Start();

        backend.SimulateWebMessage(Invoke("7", "missing"));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("error", reply.GetProperty("type").GetString());
        Assert.Equal("7", reply.GetProperty("id").GetString());
        Assert.Equal("Method not found: missing", reply.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invoke_HandlerThrows_RepliesWithItsMessage()
    {
        var (_, backend) = Start(b => b.Handle<string>("fail", () => throw new InvalidOperationException("boom")));

        backend.SimulateWebMessage(Invoke("1", "fail"));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("error", reply.GetProperty("type").GetString());
        Assert.Equal("boom", reply.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Invoke_RunsTheHandlerOffTheUiThread()
    {
        var handlerThread = 0;
        var (_, backend) = Start(b => b.Handle("where", () => handlerThread = Environment.CurrentManagedThreadId));

        backend.SimulateWebMessage(Invoke("1", "where"));
        await SentAsync(backend, 1);

        Assert.NotEqual(0, handlerThread);
        Assert.NotEqual(backend.UIThreadId, handlerThread);
    }

    [Fact]
    public async Task Invoke_ASlowHandlerDoesNotBlockTheNextInvoke()
    {
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, backend) = Start(b =>
        {
            b.HandleAsync("slow", () => release.Task);
            b.Handle("fast", () => "fast");
        });

        backend.SimulateWebMessage(Invoke("1", "slow"));
        backend.SimulateWebMessage(Invoke("2", "fast"));

        var first = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("2", first.GetProperty("id").GetString());

        release.SetResult("slow");
        var replies = await SentAsync(backend, 2);
        Assert.Equal("1", replies[1].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Invoke_RepliesThroughTheUiThreadQueue()
    {
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, backend) = Start(b => b.Handle("ping", () =>
        {
            handled.SetResult();
            return "pong";
        }));

        backend.SimulateWebMessage(Invoke("1", "ping"));
        await handled.Task.WaitAsync(Wait);
        await Task.Delay(100);

        Assert.Empty(backend.Recording.WebMessagesSent);
        Assert.Single(await SentAsync(backend, 1));
    }

    [Fact]
    public async Task HandleAsync_TokenIsCancelledWhenTheWindowCloses()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, backend) = Start(b => b.HandleAsync("wait", async ct =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                cancelled.SetResult();
                throw;
            }
        }));

        backend.SimulateWebMessage(Invoke("1", "wait"));
        await started.Task.WaitAsync(Wait);
        backend.Close();

        await cancelled.Task.WaitAsync(Wait);
    }

    [Fact]
    public async Task Events_AreDeliveredInArrivalOrder()
    {
        var received = new List<int>();
        var (_, backend) = Start(b => b.On<int>("n", n =>
        {
            lock (received)
                received.Add(n);
        }));

        for (var i = 1; i <= 20; i++)
            backend.SimulateWebMessage(Event("n", i.ToString(CultureInfo.InvariantCulture)));

        await WaitUntilAsync(() => { lock (received) return received.Count == 20; });
        Assert.Equal(Enumerable.Range(1, 20), received);
    }

    [Fact]
    public async Task On_AsyncHandler_FinishesBeforeTheNextEventStarts()
    {
        var log = new List<string>();
        var (_, backend) = Start(b => b.On<int>("n", async n =>
        {
            lock (log)
                log.Add($"start {n}");
            await Task.Delay(30);
            lock (log)
                log.Add($"end {n}");
        }));

        backend.SimulateWebMessage(Event("n", "1"));
        backend.SimulateWebMessage(Event("n", "2"));

        await WaitUntilAsync(() => { lock (log) return log.Count == 4; });
        Assert.Equal(["start 1", "end 1", "start 2", "end 2"], log);
    }

    [Fact]
    public async Task On_DisposingTheSubscription_StopsDelivery()
    {
        var pings = 0;
        var markers = 0;
        var (bridge, backend) = Start();
        var subscription = bridge.On("ping", () => Interlocked.Increment(ref pings));
        bridge.On("marker", () => Interlocked.Increment(ref markers));

        backend.SimulateWebMessage(Event("ping"));
        await WaitUntilAsync(() => Volatile.Read(ref pings) == 1);
        subscription.Dispose();
        backend.SimulateWebMessage(Event("ping"));
        backend.SimulateWebMessage(Event("marker"));
        await WaitUntilAsync(() => Volatile.Read(ref markers) == 1);

        Assert.Equal(1, Volatile.Read(ref pings));
    }

    [Fact]
    public async Task Event_WithoutDataForAValueTypeHandler_IsSkippedAndLaterMessagesStillFlow()
    {
        var received = new List<int>();
        var (_, backend) = Start(b => b.On<int>("n", n =>
        {
            lock (received)
                received.Add(n);
        }));

        backend.SimulateWebMessage("""{"type":"event","name":"n"}""");
        backend.SimulateWebMessage(Event("n", "5"));

        await WaitUntilAsync(() => { lock (received) return received.Count == 1; });
        Assert.Equal([5], received);
    }

    [Fact]
    public async Task Send_SerializesAnObjectPayloadAsCamelCase()
    {
        var (bridge, backend) = Start();

        bridge.Send("person", new Person("Ada", 36));

        var message = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("event", message.GetProperty("type").GetString());
        Assert.Equal("person", message.GetProperty("name").GetString());
        Assert.Equal("""{"name":"Ada","age":36}""", message.GetProperty("data").GetRawText());
    }

    [Fact]
    public async Task Send_WithoutData_SendsNull()
    {
        var (bridge, backend) = Start();

        bridge.Send("ping");

        var message = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal(JsonValueKind.Null, message.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Send_FromABackgroundThread_IsSentThroughTheUiThreadQueue()
    {
        var (bridge, backend) = Start();

        await Task.Run(() => bridge.Send("tick", 1));

        Assert.Empty(backend.Recording.WebMessagesSent);
        Assert.Single(await SentAsync(backend, 1));
    }

    [Fact]
    public void WithoutReflection_MissingMetadata_ErrorNamesTheTypeAndTheFix()
    {
        var (bridge, _) = Start(useReflection: false);

        var error = Assert.Throws<NotSupportedException>(() => bridge.Send("person", new Person("Ada", 36)));

        Assert.Contains(typeof(Person).FullName!, error.Message);
        Assert.Contains("JsonSerializerContext", error.Message);
    }

    [Fact]
    public async Task WithoutReflection_AnAddedContext_SuppliesTheMetadata()
    {
        var (_, backend) = Start(
            b =>
            {
                b.SerializerOptions.TypeInfoResolverChain.Add(BridgeTestJsonContext.Default);
                b.Handle("person", () => new Person("Ada", 36));
            },
            useReflection: false);

        backend.SimulateWebMessage(Invoke("1", "person"));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("""{"name":"Ada","age":36}""", reply.GetProperty("value").GetRawText());
    }

    [Fact]
    public void Handle_SameMethodTwice_Throws()
    {
        var (bridge, _) = Start(b => b.Handle("one", () => 1));

        Assert.Throws<ArgumentException>(() => bridge.Handle("one", () => 2));
    }

    [Fact]
    public void Stop_ReturnsAfterTheTimeout_WhenAHandlerNeverFinishes()
    {
        var never = new TaskCompletionSource<int>();
        var started = new ManualResetEventSlim();
        var backend = new RecordingWindowBackend();
        var bridge = new InteropBridge(backend) { StopTimeout = TimeSpan.FromMilliseconds(200) };
        bridge.HandleAsync("hang", () =>
        {
            started.Set();
            return never.Task;
        });
        bridge.Start();

        backend.SimulateWebMessage(Invoke("1", "hang"));
        Assert.True(started.Wait(Wait));
        var stopwatch = Stopwatch.StartNew();
        bridge.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Stop took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task NonBridgeMessages_AreIgnored()
    {
        var (_, backend) = Start(b => b.Handle("ping", () => "pong"));

        backend.SimulateWebMessage("not json");
        backend.SimulateWebMessage("""{"type":"hermes-drag","action":"drag"}""");
        backend.SimulateWebMessage(Invoke("1", "ping"));

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("pong", reply.GetProperty("value").GetString());
        await Task.Delay(50);
        backend.ProcessPending();
        Assert.Single(backend.Recording.WebMessagesSent);
    }

    [Fact]
    public async Task MessagesBeforeStart_AreHandledOnceStarted()
    {
        var backend = new RecordingWindowBackend();
        var bridge = new InteropBridge(backend);
        _bridges.Add(bridge);
        bridge.Handle("ping", () => "pong");

        backend.SimulateWebMessage(Invoke("1", "ping"));
        bridge.Start();

        var reply = Assert.Single(await SentAsync(backend, 1));
        Assert.Equal("pong", reply.GetProperty("value").GetString());
    }

    private (InteropBridge Bridge, RecordingWindowBackend Backend) Start(
        Action<InteropBridge>? configure = null,
        bool useReflection = true)
    {
        var backend = new RecordingWindowBackend();
        var bridge = new InteropBridge(backend, useReflection);
        _bridges.Add(bridge);
        configure?.Invoke(bridge);
        bridge.Start();
        return (bridge, backend);
    }

    private static string Invoke(string id, string method, string args = "[]") =>
        $$"""{"type":"invoke","id":"{{id}}","method":"{{method}}","args":{{args}}}""";

    private static string Event(string name, string data = "null") =>
        $$"""{"type":"event","name":"{{name}}","data":{{data}}}""";

    /// <summary>Drains the UI-thread queue until <paramref name="count"/> messages have been sent.</summary>
    private static async Task<IReadOnlyList<JsonElement>> SentAsync(RecordingWindowBackend backend, int count)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (DateTime.UtcNow < deadline)
        {
            backend.ProcessPending();
            if (backend.Recording.WebMessagesSent.Count >= count)
                break;
            await Task.Delay(5);
        }

        var sent = backend.Recording.WebMessagesSent;
        Assert.True(sent.Count >= count, $"Expected {count} sent message(s), got {sent.Count}.");
        return sent.Select(m => JsonDocument.Parse(m).RootElement.Clone()).ToList();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the bridge.");
            await Task.Delay(5);
        }
    }
}

public sealed record Person(string Name, int Age);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(Person))]
internal sealed partial class BridgeTestJsonContext : JsonSerializerContext;
