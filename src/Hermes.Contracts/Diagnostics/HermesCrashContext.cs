// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Contracts.Diagnostics;

/// <summary>
/// Structured context for a crash event.
/// </summary>
public record HermesCrashContext(
    HermesExceptionInfo Exception,
    HermesPlatformInfo Platform,
    DateTimeOffset CrashedAt,
    CrashSource Source,
    string? AnonymousSessionId = null,
    IDictionary<string, string>? AdditionalContext = null);
