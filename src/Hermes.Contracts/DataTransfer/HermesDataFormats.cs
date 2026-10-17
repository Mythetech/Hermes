// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.DataTransfer;

/// <summary>
/// Standard formats a <see cref="HermesDataPackage"/> can carry. Custom formats are named by MIME type instead.
/// </summary>
[Flags]
public enum HermesDataFormats
{
    None = 0,
    Text = 1,
    Html = 2,
    Png = 4,
    Files = 8,
    Url = 16,
}
