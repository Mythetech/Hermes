// Copyright (c) Mythetech. Licensed under the MIT License.
namespace SmokeTestApp;

/// <summary>The deliberate fault a run selects, so CI can prove each failure path is caught.</summary>
public enum SmokeFaultKind
{
    None,
    Check,
    Render,
    Gate,
    Log,
    Dialog,
}

public sealed record SmokeFault(SmokeFaultKind Kind)
{
    // A sample-only variable; it is not part of the Hermes smoke contract.
    public static SmokeFault FromEnvironment() =>
        new(Environment.GetEnvironmentVariable("SMOKE_SAMPLE_FAULT")?.Trim().ToLowerInvariant() switch
        {
            "check" => SmokeFaultKind.Check,
            "render" => SmokeFaultKind.Render,
            "gate" => SmokeFaultKind.Gate,
            "log" => SmokeFaultKind.Log,
            "dialog" => SmokeFaultKind.Dialog,
            _ => SmokeFaultKind.None,
        });
}
