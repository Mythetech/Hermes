// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Licensing;

internal sealed record LicenseValidationResult(LicenseStatus Status, string? Message = null)
{
    internal static LicenseValidationResult Valid() => new(LicenseStatus.Valid);
    internal static LicenseValidationResult NoKey() => new(LicenseStatus.NoKey);
    internal static LicenseValidationResult InvalidFormat(string reason) => new(LicenseStatus.InvalidFormat, reason);
    internal static LicenseValidationResult InvalidSignature() => new(LicenseStatus.InvalidSignature, "Token signature is invalid.");
    internal static LicenseValidationResult AssemblyMismatch() => new(LicenseStatus.AssemblyMismatch, "Token is bound to a different assembly.");
    internal static LicenseValidationResult VersionNotCovered() => new(LicenseStatus.VersionNotCovered, "This Hermes version was released after the subscription ended.");
}
