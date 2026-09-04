// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;

namespace Hermes.Tests.Web.WebView;

/// <summary>
/// Runs work items synchronously, which is what HermesDispatcher does when it is already on
/// the UI thread. Exceptions therefore escape synchronously, the same way they do in production.
/// </summary>
internal sealed class InlineDispatcher : Dispatcher
{
    public override bool CheckAccess() => true;

    public override Task InvokeAsync(Action workItem)
    {
        workItem();
        return Task.CompletedTask;
    }

    public override Task InvokeAsync(Func<Task> workItem) => workItem();

    public override Task<TResult> InvokeAsync<TResult>(Func<TResult> workItem) => Task.FromResult(workItem());

    public override Task<TResult> InvokeAsync<TResult>(Func<Task<TResult>> workItem) => workItem();
}
