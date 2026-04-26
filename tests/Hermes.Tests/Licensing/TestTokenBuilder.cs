// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hermes.Tests.Licensing;

internal static class TestTokenBuilder
{
    private const int PayloadSize = 38;
    private const int SignatureSize = 64;
    private const string Prefix = "HERMES-1-";

    internal static string BuildFreeToken(
        Guid? customerId = null,
        string productSlug = "hermes")
    {
        return BuildToken(
            formatVersion: 1,
            licenseType: 0x01,
            customerId: customerId ?? Guid.NewGuid(),
            productSlug: productSlug,
            subscriptionEnd: 0xFFFFFFFF,
            assemblyNameHash: 0UL,
            issuedAt: (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    internal static string BuildPaidToken(
        string assemblyName,
        DateTime subscriptionEnd,
        Guid? customerId = null,
        string productSlug = "hermes")
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(assemblyName));
        var assemblyHash = BinaryPrimitives.ReadUInt64BigEndian(hashBytes);
        var endTimestamp = (uint)new DateTimeOffset(subscriptionEnd, TimeSpan.Zero).ToUnixTimeSeconds();

        return BuildToken(
            formatVersion: 1,
            licenseType: 0x02,
            customerId: customerId ?? Guid.NewGuid(),
            productSlug: productSlug,
            subscriptionEnd: endTimestamp,
            assemblyNameHash: assemblyHash,
            issuedAt: (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    internal static string BuildToken(
        byte formatVersion,
        byte licenseType,
        Guid customerId,
        string productSlug,
        uint subscriptionEnd,
        ulong assemblyNameHash,
        uint issuedAt)
    {
        var payload = new byte[PayloadSize];
        payload[0] = formatVersion;
        payload[1] = licenseType;
        customerId.TryWriteBytes(payload.AsSpan(2));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(18), Crc32(Encoding.UTF8.GetBytes(productSlug)));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(22), subscriptionEnd);
        BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(26), assemblyNameHash);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(34), issuedAt);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ecdsa.ImportECPrivateKey(TestLicenseKeys.PrivateKey, out _);
        var signature = ecdsa.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var full = new byte[PayloadSize + SignatureSize];
        payload.CopyTo(full, 0);
        signature.CopyTo(full, PayloadSize);

        return Prefix + Base64UrlEncode(full);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 * (crc & 1));
        }
        return ~crc;
    }
}
