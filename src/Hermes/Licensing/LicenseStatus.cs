// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Licensing;

internal enum LicenseStatus
{
    Valid,
    NoKey,
    InvalidFormat,
    InvalidSignature,
    AssemblyMismatch,
    VersionNotCovered
}
