// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Contracts.Diagnostics;

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
