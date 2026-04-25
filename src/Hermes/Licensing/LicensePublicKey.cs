// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Licensing;

internal static class LicensePublicKey
{
    // Placeholder: replace with real ECDSA P-256 public key (SPKI-encoded) from Platform Phase 1.
    // The empty key here will cause all tokens to fail signature validation until replaced.
    internal static readonly IReadOnlyDictionary<byte, byte[]> Keys = new Dictionary<byte, byte[]>
    {
        [1] = new byte[91]
    };
}
