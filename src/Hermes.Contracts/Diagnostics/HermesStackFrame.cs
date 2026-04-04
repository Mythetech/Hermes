// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Contracts.Diagnostics;

/// <summary>
/// A single frame from a stack trace.
/// </summary>
public record HermesStackFrame(
    string? FileName,
    string? MethodName,
    string? TypeName,
    int? LineNumber,
    int? ColumnNumber);
