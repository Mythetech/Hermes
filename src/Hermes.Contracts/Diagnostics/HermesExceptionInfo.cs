// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Contracts.Diagnostics;

/// <summary>
/// Exception details captured from a crash.
/// </summary>
public record HermesExceptionInfo(
    string ExceptionType,
    string Message,
    IReadOnlyList<HermesStackFrame> StackTrace,
    HermesExceptionInfo? InnerException = null);
