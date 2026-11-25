using System.Buffers;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskDisplay.Display.Services;

/// <summary>Uniform, opaque native video on an opaque canvas. No nonuniform stretching or crop-mode substitutions.</summary>
internal sealed class VideoCanvasNormalizer : IDisposable
{
    private readonly record struct Axis(int Low,int High,double Weight,double Coverage);
    private byte[]? _input;
    private (int Sw,int Sh,int Dw,int Dh,double X,double Y) _geometry;
    private Axis[]? _xs,_ys;

    public void Copy(BitmapSource source,byte[] destination,int width,int height,Color background,double cropX,double cropY)
    {
        var length=checked(width*height*4);var sourceLength=checked(source.PixelWidth*source.PixelHeight*4);
        if(source.Format!=PixelFormats.Bgra32||background.A!=255||destination.Length<length)throw new ArgumentException("Unsupported normalization format.");
        var geometry=(source.PixelWidth,source.PixelHeight,width,height,cropX,cropY);
        if(_xs is null||_geometry!=geometry)
        {
            var scale=Math.Min(width/(double)source.PixelWidth,height/(double)source.PixelHeight);
            if(scale<1)throw new ArgumentException("Downsampled frames retain the reference renderer.");
            var imageWidth=source.PixelWidth*scale;var imageHeight=source.PixelHeight*scale;
            _xs=BuildAxis(source.PixelWidth,width,(width-imageWidth)*Math.Clamp(cropX,0,1),scale);
            _ys=BuildAxis(source.PixelHeight,height,(height-imageHeight)*Math.Clamp(cropY,0,1),scale);_geometry=geometry;
        }
        if(_input is null||_input.Length<sourceLength)
        {var next=ArrayPool<byte>.Shared.Rent(sourceLength);if(_input is {} old)ArrayPool<byte>.Shared.Return(old);_input=next;}
        source.CopyPixels(_input,source.PixelWidth*4,0);
        var color=(uint)(background.B|(background.G<<8)|(background.R<<16)|(255u<<24));
        MemoryMarshal.Cast<byte,uint>(destination.AsSpan(0,length)).Fill(color);
        for(var y=0;y<height;y++)
        {
            var vertical=_ys![y];if(vertical.Coverage==0)continue;
            for(var x=0;x<width;x++)
            {
                var horizontal=_xs[x];var coverage=horizontal.Coverage*vertical.Coverage;if(coverage==0)continue;
                var a=(vertical.Low*source.PixelWidth+horizontal.Low)*4;var b=(vertical.Low*source.PixelWidth+horizontal.High)*4;
                var c=(vertical.High*source.PixelWidth+horizontal.Low)*4;var d=(vertical.High*source.PixelWidth+horizontal.High)*4;var output=(y*width+x)*4;
                if(vertical.Weight==0&&vertical.Coverage==1)
                {
                    // 高度已匹配画布时，不再对相同源行做垂直双线性插值。
                    if(horizontal.Weight==0&&coverage==1)
                    {destination[output]=_input[a];destination[output+1]=_input[a+1];destination[output+2]=_input[a+2];continue;}
                    for(var channel=0;channel<3;channel++)
                    {
                        var value=(_input[a+channel]*(1-horizontal.Weight)+_input[b+channel]*horizontal.Weight)*coverage+destination[output+channel]*(1-coverage);
                        destination[output+channel]=(byte)Math.Clamp((int)Math.Round(value),0,255);
                    }
                    continue;
                }
                for(var channel=0;channel<3;channel++)
                {
                    var top=_input[a+channel]*(1-horizontal.Weight)+_input[b+channel]*horizontal.Weight;
                    var bottom=_input[c+channel]*(1-horizontal.Weight)+_input[d+channel]*horizontal.Weight;
                    var value=(top*(1-vertical.Weight)+bottom*vertical.Weight)*coverage+destination[output+channel]*(1-coverage);
                    destination[output+channel]=(byte)Math.Clamp((int)Math.Round(value),0,255);
                }
            }
        }
    }

    private static Axis[] BuildAxis(int source,int target,double start,double scale)
    {
        var result=new Axis[target];var end=start+source*scale;
        for(var i=0;i<target;i++)
        {
            var coverage=Math.Clamp(Math.Min(i+1d,end)-Math.Max(i,start),0,1);
            var position=Math.Clamp((i+.5-start)/scale-.5,0,source-1d);var low=(int)Math.Floor(position);
            result[i]=new(low,Math.Min(source-1,low+1),position-low,coverage);
        }
        return result;
    }
    public void Dispose(){if(_input is {} input){_input=null;ArrayPool<byte>.Shared.Return(input);}_xs=_ys=null;}
}
