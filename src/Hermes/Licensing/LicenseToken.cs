// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Licensing;

internal sealed record LicenseToken(
    byte FormatVersion,
    byte LicenseType,
    Guid CustomerId,
    uint ProductSlugHash,
    uint SubscriptionEnd,
    ulong AssemblyNameHash,
    uint IssuedAt)
{
    internal const byte TypeFree = 0x01;
    internal const byte TypePaid = 0x02;
    internal const uint NeverExpires = 0xFFFFFFFF;
}
