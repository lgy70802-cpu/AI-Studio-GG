# Read-only active display topology; reuse the application's Windows interop declarations.
$ErrorActionPreference = 'Stop'
if (-not ('DeskDisplay.Display.Interop.TopologyInspector' -as [type])) {
    $nativePath = Join-Path $PSScriptRoot '..\src\DeskDisplay.Display\Interop\NativeMethods.cs'
    $native = Get-Content -LiteralPath $nativePath -Raw
    $native = $native.Replace('namespace DeskDisplay.Display.Interop;', 'namespace DeskDisplay.Display.Interop {') + "`n}"
    Add-Type -TypeDefinition ("using System;`n" + $native + @'

namespace DeskDisplay.Display.Interop {
 public static class TopologyInspector {
  public static object[] Read() {
   for(int attempt=0;attempt<3;attempt++) {
    uint pc,mc;
    int error=NativeMethods.GetDisplayConfigBufferSizes(2,out pc,out mc);
    if(error!=0) throw new System.ComponentModel.Win32Exception(error);
    var paths=new NativeMethods.PathInfo[Math.Max(pc,1)];
    var modes=new NativeMethods.ModeInfo[Math.Max(mc,1)];
    error=NativeMethods.QueryDisplayConfig(2,ref pc,paths,ref mc,modes,IntPtr.Zero);
    if(error==122) continue;
    if(error!=0) throw new System.ComponentModel.Win32Exception(error);
    var result=new System.Collections.Generic.List<object>();
    for(int i=0;i<pc;i++) {
     var p=paths[i];
     var s=new NativeMethods.SourceDeviceName();
     s.Header.Type=1; s.Header.Size=(uint)Marshal.SizeOf<NativeMethods.SourceDeviceName>();
     s.Header.AdapterId=p.Source.AdapterId; s.Header.Id=p.Source.Id;
     error=NativeMethods.GetSourceDeviceName(ref s);
     if(error!=0) throw new System.ComponentModel.Win32Exception(error);
     var t=new NativeMethods.TargetDeviceName();
     t.Header.Type=2; t.Header.Size=(uint)Marshal.SizeOf<NativeMethods.TargetDeviceName>();
     t.Header.AdapterId=p.Target.AdapterId; t.Header.Id=p.Target.Id;
     error=NativeMethods.GetTargetDeviceName(ref t);
     if(error!=0) throw new System.ComponentModel.Win32Exception(error);
     if(p.Source.ModeInfoIndex>=mc) throw new InvalidOperationException("No source mode in snapshot");
     var m=modes[p.Source.ModeInfoIndex];
     if(m.InfoType!=1) throw new InvalidOperationException("Unexpected mode type");
     result.Add(new { Name=t.FriendlyName, Source=s.Name, DevicePath=t.DevicePath,
      Width=m.Width, Height=m.Height, X=m.X, Y=m.Y, Rotation=p.Target.Rotation,
      RefreshNumerator=p.Target.RefreshRate.Numerator, RefreshDenominator=p.Target.RefreshRate.Denominator,
      OutputTechnology=p.Target.OutputTechnology });
    }
    return result.ToArray();
   }
   throw new InvalidOperationException("Display topology kept changing");
  }
 }
}
'@)
}
[DeskDisplay.Display.Interop.TopologyInspector]::Read() | ConvertTo-Json -Depth 4
