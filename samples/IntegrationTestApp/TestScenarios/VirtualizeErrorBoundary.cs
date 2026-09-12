// Copyright (c) Mythetech. Licensed under the MIT License.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// An <see cref="ErrorBoundary"/> that routes captured exceptions to the <see cref="VirtualizeProbe"/>,
/// so a Virtualize interop failure is reported as a named scenario failure instead of a silent
/// error UI.
/// </summary>
public sealed class VirtualizeErrorBoundary : ErrorBoundary
{
    [Inject] private VirtualizeProbe Probe { get; set; } = default!;

    protected override async Task OnErrorAsync(Exception exception)
    {
        // A JSException carries the whole JS stack in its Message, which would wrap the reporter's
        // summary table over a dozen lines. The log keeps the full trace; the scenario result keeps
        // the one line that names the missing interop entry point.
        Console.WriteLine($"VIRTUALIZE_INTEROP_ERROR: {exception}");

        var summary = exception.Message.Split('\n', 2)[0].Trim();
        Probe.ReportError(summary);

        await base.OnErrorAsync(exception);
    }
}
