// Copyright (c) Mythetech. Licensed under the MIT License.
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
