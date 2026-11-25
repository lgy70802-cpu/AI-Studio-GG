namespace DeskDisplay.Display.Services;

internal readonly record struct PixelAlphaBounds(int X,int Y,int Width,int Height)
{
    public int Area=>Width*Height;
}

/// <summary>Premultiplied foreground over opaque BGRA video. Bounds include every nonzero alpha pixel.</summary>
internal static class VideoOverlayBlender
{
    public static PixelAlphaBounds FindBounds(ReadOnlySpan<byte> overlay,int width,int height)
    {
        if(width<1||height<1||overlay.Length<checked(width*height*4))throw new ArgumentException("Invalid overlay geometry.");
        var left=width;var top=height;var right=-1;var bottom=-1;
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)
        {
            if(overlay[(y*width+x)*4+3]==0)continue;
            left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=y;
        }
        return right<left?default:new(left,top,right-left+1,bottom-top+1);
    }

    public static void Blend(Span<byte> pixels,ReadOnlySpan<byte> overlay,int width,int height,PixelAlphaBounds bounds)
    {
        var length=checked(width*height*4);
        if(width<1||height<1||pixels.Length<length||overlay.Length<length||bounds.X<0||bounds.Y<0||bounds.Width<0||bounds.Height<0||bounds.X+bounds.Width>width||bounds.Y+bounds.Height>height)throw new ArgumentException("Invalid blend geometry.");
        for(var y=bounds.Y;y<bounds.Y+bounds.Height;y++)
        for(var x=bounds.X;x<bounds.X+bounds.Width;x++)
        {
            var i=(y*width+x)*4;var alpha=overlay[i+3];if(alpha==0)continue;var remaining=255-alpha;
            pixels[i]=(byte)Math.Min(255,overlay[i]+(pixels[i]*remaining+127)/255);
            pixels[i+1]=(byte)Math.Min(255,overlay[i+1]+(pixels[i+1]*remaining+127)/255);
            pixels[i+2]=(byte)Math.Min(255,overlay[i+2]+(pixels[i+2]*remaining+127)/255);
            pixels[i+3]=255;
        }
    }
}
