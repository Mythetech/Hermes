// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Diagnostics;

/// <summary>
/// Structured context for a crash event, passed to <see cref="HermesCrashInterceptor.OnCrash"/>.
/// </summary>
public record HermesCrashContext(
    HermesExceptionInfo Exception,
    HermesPlatformInfo Platform,
    DateTimeOffset CrashedAt,
    CrashSource Source,
    string? AnonymousSessionId = null,
    IDictionary<string, string>? AdditionalContext = null);

/// <summary>
/// Exception details captured from a crash.
/// </summary>
public record HermesExceptionInfo(
    string ExceptionType,
    string Message,
    IReadOnlyList<HermesStackFrame> StackTrace,
    HermesExceptionInfo? InnerException = null);

/// <summary>
/// Platform and product information captured at crash time.
/// </summary>
public record HermesPlatformInfo(
    string ProductName,
    string ProductVersion,
    string OperatingSystem,
    string OsVersion,
    string Architecture,
    string? DotNetVersion = null,
    string? DeviceModel = null);

/// <summary>
/// A single frame from a stack trace.
/// </summary>
public record HermesStackFrame(
    string? FileName,
    string? MethodName,
    string? TypeName,
    int? LineNumber,
    int? ColumnNumber);
