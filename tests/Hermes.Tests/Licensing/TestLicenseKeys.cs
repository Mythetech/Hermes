// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Security.Cryptography;

namespace Hermes.Tests.Licensing;

internal static class TestLicenseKeys
{
    static TestLicenseKeys()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PrivateKey = ecdsa.ExportECPrivateKey();
        PublicKey = ecdsa.ExportSubjectPublicKeyInfo();
    }

    internal static byte[] PublicKey { get; }
    internal static byte[] PrivateKey { get; }

    internal static IReadOnlyDictionary<byte, byte[]> KeyDictionary => new Dictionary<byte, byte[]>
    {
        [1] = PublicKey
    };
}
