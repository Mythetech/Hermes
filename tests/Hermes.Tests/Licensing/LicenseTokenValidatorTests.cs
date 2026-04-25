// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Licensing;
using Xunit;

namespace Hermes.Tests.Licensing;

public sealed class LicenseTokenValidatorTests
{
    private static readonly DateTime TestReleaseDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private LicenseValidationResult Validate(string? token, string assemblyName = "TestApp")
        => LicenseTokenValidator.Validate(token, assemblyName, TestReleaseDate, TestLicenseKeys.KeyDictionary);

    [Fact]
    public void Null_token_returns_NoKey()
    {
        var result = Validate(null);
        Assert.Equal(LicenseStatus.NoKey, result.Status);
    }

    [Fact]
    public void Empty_token_returns_NoKey()
    {
        var result = Validate("");
        Assert.Equal(LicenseStatus.NoKey, result.Status);
    }

    [Fact]
    public void Garbage_input_returns_InvalidFormat()
    {
        var result = Validate("not-a-token");
        Assert.Equal(LicenseStatus.InvalidFormat, result.Status);
    }

    [Fact]
    public void Wrong_prefix_returns_InvalidFormat()
    {
        var result = Validate("NOTHERMES-1-AAAA");
        Assert.Equal(LicenseStatus.InvalidFormat, result.Status);
    }

    [Fact]
    public void Wrong_length_returns_InvalidFormat()
    {
        var result = Validate("HERMES-1-" + Convert.ToBase64String(new byte[50]).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
        Assert.Equal(LicenseStatus.InvalidFormat, result.Status);
    }

    [Fact]
    public void Unknown_version_byte_returns_InvalidFormat()
    {
        var token = TestTokenBuilder.BuildToken(
            formatVersion: 99,
            licenseType: 0x01,
            customerId: Guid.NewGuid(),
            productSlug: "hermes",
            subscriptionEnd: 0xFFFFFFFF,
            assemblyNameHash: 0UL,
            issuedAt: (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var result = Validate(token);
        Assert.Equal(LicenseStatus.InvalidFormat, result.Status);
    }

    [Fact]
    public void Tampered_token_returns_InvalidSignature()
    {
        var token = TestTokenBuilder.BuildFreeToken();

        // Flip a byte in the payload section (after prefix, within the base64url-encoded data)
        var chars = token.ToCharArray();
        var payloadStart = "HERMES-1-".Length;
        chars[payloadStart + 2] = chars[payloadStart + 2] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        var result = Validate(tampered);
        Assert.True(
            result.Status == LicenseStatus.InvalidSignature || result.Status == LicenseStatus.InvalidFormat,
            $"Expected InvalidSignature or InvalidFormat, got {result.Status}");
    }

    [Fact]
    public void Valid_free_token_returns_Valid()
    {
        var token = TestTokenBuilder.BuildFreeToken();

        var result = Validate(token);
        Assert.Equal(LicenseStatus.Valid, result.Status);
    }

    [Fact]
    public void Valid_paid_token_matching_assembly_returns_Valid()
    {
        var token = TestTokenBuilder.BuildPaidToken(
            assemblyName: "TestApp",
            subscriptionEnd: DateTime.UtcNow.AddYears(1));

        var result = Validate(token, assemblyName: "TestApp");
        Assert.Equal(LicenseStatus.Valid, result.Status);
    }

    [Fact]
    public void Paid_token_wrong_assembly_returns_AssemblyMismatch()
    {
        var token = TestTokenBuilder.BuildPaidToken(
            assemblyName: "CorrectApp",
            subscriptionEnd: DateTime.UtcNow.AddYears(1));

        var result = Validate(token, assemblyName: "WrongApp");
        Assert.Equal(LicenseStatus.AssemblyMismatch, result.Status);
    }

    [Fact]
    public void Expired_paid_token_with_perpetual_rights_returns_Valid()
    {
        // Subscription ended 2025-06-01 (in the past), release date 2025-04-01 (before sub end)
        var token = TestTokenBuilder.BuildPaidToken(
            assemblyName: "TestApp",
            subscriptionEnd: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = LicenseTokenValidator.Validate(
            token, "TestApp",
            releaseDate: new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            TestLicenseKeys.KeyDictionary);

        Assert.Equal(LicenseStatus.Valid, result.Status);
    }

    [Fact]
    public void Expired_paid_token_without_perpetual_rights_returns_VersionNotCovered()
    {
        // Subscription ended 2025-03-01 (in the past), release date 2025-06-01 (after sub end)
        var token = TestTokenBuilder.BuildPaidToken(
            assemblyName: "TestApp",
            subscriptionEnd: new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = LicenseTokenValidator.Validate(
            token, "TestApp",
            releaseDate: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            TestLicenseKeys.KeyDictionary);

        Assert.Equal(LicenseStatus.VersionNotCovered, result.Status);
    }

    [Fact]
    public void Unknown_license_type_returns_InvalidFormat()
    {
        var token = TestTokenBuilder.BuildToken(
            formatVersion: 1,
            licenseType: 0x99,
            customerId: Guid.NewGuid(),
            productSlug: "hermes",
            subscriptionEnd: 0xFFFFFFFF,
            assemblyNameHash: 0UL,
            issuedAt: (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var result = Validate(token);
        Assert.Equal(LicenseStatus.InvalidFormat, result.Status);
    }
}
