using System.Buffers;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace DeskDisplay.Display.Services;

/// <summary>One serialized encoder's scratch storage. Returned images own frozen native pixel snapshots.</summary>
internal sealed class VideoRotationBuffer : IDisposable
{
    private byte[]? _input,_output;
    private bool _disposed;
    private WriteableBitmap? _privateBitmap;
    internal int RetainedCapacity=>(_input?.Length??0)+(_output?.Length??0);

    public BitmapSource Rotate(BitmapSource bitmap,int rotation,bool reuseEncoderBitmap=false)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(rotation==0)return bitmap;
        if(rotation is not(90 or 180 or 270))throw new ArgumentOutOfRangeException(nameof(rotation));
        if(bitmap.Format.BitsPerPixel!=32)throw new NotSupportedException("Rotation requires 32-bit pixels.");
        var width=bitmap.PixelWidth;var height=bitmap.PixelHeight;var length=checked(width*height*4);
        Ensure(ref _input,length);Ensure(ref _output,length);
        bitmap.CopyPixels(_input!,width*4,0);
        var source=MemoryMarshal.Cast<byte,uint>(_input.AsSpan(0,length));var destination=MemoryMarshal.Cast<byte,uint>(_output.AsSpan(0,length));
        if(rotation==180)
        {
            for(var i=0;i<source.Length;i++)destination[source.Length-1-i]=source[i];
        }
        else
        {
            const int tile=32;
            for(var tileY=0;tileY<height;tileY+=tile)
            for(var tileX=0;tileX<width;tileX+=tile)
            for(var y=tileY;y<Math.Min(height,tileY+tile);y++)
            for(var x=tileX;x<Math.Min(width,tileX+tile);x++)
            {
                var dx=rotation==90?height-1-y:y;
                var dy=rotation==90?x:width-1-x;
                destination[dy*height+dx]=source[y*width+x];
            }
        }
        var targetWidth=rotation==180?width:height;
        if(reuseEncoderBitmap)
        {
            var targetHeight=rotation==180?height:width;
            if(_privateBitmap is null||_privateBitmap.PixelWidth!=targetWidth||_privateBitmap.PixelHeight!=targetHeight||_privateBitmap.Format!=bitmap.Format)
                _privateBitmap=new WriteableBitmap(targetWidth,targetHeight,96,96,bitmap.Format,null);
            _privateBitmap.WritePixels(new System.Windows.Int32Rect(0,0,targetWidth,targetHeight),_output!,targetWidth*4,0);
            return _privateBitmap;
        }
        var result=BitmapSource.Create(targetWidth,rotation==180?height:width,96,96,bitmap.Format,null,_output!,targetWidth*4);
        result.Freeze();return result;
    }

    private static void Ensure(ref byte[]? buffer,int length)
    {
        if(buffer is not null&&buffer.Length>=length)return;
        var replacement=ArrayPool<byte>.Shared.Rent(length);var previous=buffer;buffer=replacement;
        if(previous is not null)ArrayPool<byte>.Shared.Return(previous);
    }

    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_privateBitmap=null;
        if(_input is {} input){_input=null;ArrayPool<byte>.Shared.Return(input);}
        if(_output is {} output){_output=null;ArrayPool<byte>.Shared.Return(output);}
    }
}
