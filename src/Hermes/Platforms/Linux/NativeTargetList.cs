// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace Hermes.Platforms.Linux;

/// <summary>
/// Target names, data and lengths as parallel native arrays for the shim, freed on dispose.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class NativeTargetList : IDisposable
{
    public NativeTargetList(IReadOnlyList<(string Target, byte[] Data)> entries)
    {
        Count = entries.Count;
        Targets = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(Count, 1), (nuint)sizeof(byte*));
        Data = (byte**)NativeMemory.AllocZeroed((nuint)Math.Max(Count, 1), (nuint)sizeof(byte*));
        Lengths = (int*)NativeMemory.AllocZeroed((nuint)Math.Max(Count, 1), sizeof(int));

        try
        {
            for (var i = 0; i < Count; i++)
            {
                var (target, bytes) = entries[i];
                Targets[i] = Utf8StringMarshaller.ConvertToUnmanaged(target);
                Data[i] = (byte*)NativeMemory.Alloc((nuint)Math.Max(bytes.Length, 1));
                bytes.CopyTo(new Span<byte>(Data[i], bytes.Length));
                Lengths[i] = bytes.Length;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int Count { get; }

    public byte** Targets { get; }

    public byte** Data { get; }

    public int* Lengths { get; }

    public void Dispose()
    {
        for (var i = 0; i < Count; i++)
        {
            Utf8StringMarshaller.Free(Targets[i]);
            NativeMemory.Free(Data[i]);
        }
        NativeMemory.Free(Targets);
        NativeMemory.Free(Data);
        NativeMemory.Free(Lengths);
    }
}
