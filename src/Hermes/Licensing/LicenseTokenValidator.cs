// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Hermes.Licensing;

internal static class LicenseTokenValidator
{
    private const string TokenPrefix = "HERMES-1-";
    private const int PayloadSize = 38;
    private const int SignatureSize = 64;
    private const int TotalSize = PayloadSize + SignatureSize;

    internal static LicenseValidationResult Validate(
        string? token, string assemblyName, DateTime releaseDate)
        => Validate(token, assemblyName, releaseDate, LicensePublicKey.Keys);

    internal static LicenseValidationResult Validate(
        string? token, string assemblyName, DateTime releaseDate,
        IReadOnlyDictionary<byte, byte[]> publicKeys)
    {
        if (string.IsNullOrEmpty(token))
            return LicenseValidationResult.NoKey();

        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return LicenseValidationResult.InvalidFormat("Missing HERMES-1- prefix.");

        var encoded = token[TokenPrefix.Length..];
        byte[] decoded;
        try
        {
            decoded = Base64UrlDecode(encoded);
        }
        catch (FormatException)
        {
            return LicenseValidationResult.InvalidFormat("Invalid base64url encoding.");
        }

        if (decoded.Length != TotalSize)
            return LicenseValidationResult.InvalidFormat($"Expected {TotalSize} bytes, got {decoded.Length}.");

        var version = decoded[0];
        if (!publicKeys.TryGetValue(version, out var publicKey))
            return LicenseValidationResult.InvalidFormat($"Unknown token version: {version}.");

        var payload = decoded.AsSpan(0, PayloadSize);
        var signature = decoded.AsSpan(PayloadSize, SignatureSize);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
        if (!ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            return LicenseValidationResult.InvalidSignature();

        var parsedToken = Deserialize(payload);

        if (parsedToken.LicenseType == LicenseToken.TypeFree)
            return LicenseValidationResult.Valid();

        if (parsedToken.LicenseType == LicenseToken.TypePaid)
            return ValidatePaidToken(parsedToken, assemblyName, releaseDate);

        return LicenseValidationResult.InvalidFormat($"Unknown license type: {parsedToken.LicenseType}.");
    }

    private static LicenseValidationResult ValidatePaidToken(
        LicenseToken token, string assemblyName, DateTime releaseDate)
    {
        var expectedHash = BinaryPrimitives.ReadUInt64BigEndian(
            SHA256.HashData(Encoding.UTF8.GetBytes(assemblyName)));

        if (token.AssemblyNameHash != expectedHash)
            return LicenseValidationResult.AssemblyMismatch();

        if (token.SubscriptionEnd == LicenseToken.NeverExpires)
            return LicenseValidationResult.Valid();

        var endDate = DateTimeOffset.FromUnixTimeSeconds(token.SubscriptionEnd).UtcDateTime;
        if (endDate >= DateTime.UtcNow)
            return LicenseValidationResult.Valid();

        if (endDate >= releaseDate)
            return LicenseValidationResult.Valid();

        return LicenseValidationResult.VersionNotCovered();
    }

    private static LicenseToken Deserialize(ReadOnlySpan<byte> payload)
    {
        return new LicenseToken(
            FormatVersion: payload[0],
            LicenseType: payload[1],
            CustomerId: new Guid(payload.Slice(2, 16)),
            ProductSlugHash: BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(18, 4)),
            SubscriptionEnd: BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(22, 4)),
            AssemblyNameHash: BinaryPrimitives.ReadUInt64BigEndian(payload.Slice(26, 8)),
            IssuedAt: BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(34, 4)));
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
