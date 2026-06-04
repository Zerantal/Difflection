using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace Difflection.Infrastructure;

public sealed class ManagedFramebuffer : ILockedFramebuffer
{
    private GCHandle _handle;

    public ManagedFramebuffer(byte[] pixels, PixelSize size, int rowBytes)
    {
        _handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        Address = _handle.AddrOfPinnedObject();
        Size = size;
        RowBytes = rowBytes;
    }

    public IntPtr Address { get; }

    public PixelSize Size { get; }

    public int RowBytes { get; }

    public Vector Dpi { get; } = new(96, 96);

    public PixelFormat Format => PixelFormats.Bgra8888;

    public AlphaFormat AlphaFormat => AlphaFormat.Premul;

    public void Dispose()
    {
        if (!_handle.IsAllocated) return;
        _handle.Free();
        _handle = default;
    }
}
