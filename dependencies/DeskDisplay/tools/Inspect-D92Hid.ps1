# Read-only HID descriptor inspection. Does not send/read reports or change device state.
[CmdletBinding()]
param([int]$VendorId = 0x5548, [int]$ProductId = 0x1011)
$ErrorActionPreference = 'Stop'
if (-not ('DeskDisplay.Diagnostics.HidInspector' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace DeskDisplay.Diagnostics {
 public sealed class HidInfo {
  public string Path { get; set; }
  public string Product { get; set; }
  public string Manufacturer { get; set; }
  public string SerialNumber { get; set; }
  public byte[] InputReportIds { get; set; }
  public byte[] OutputReportIds { get; set; }
  public byte[] FeatureReportIds { get; set; }
  public int UsagePage { get; set; }
  public int Usage { get; set; }
  public int InputReportBytes { get; set; }
  public int OutputReportBytes { get; set; }
  public int FeatureReportBytes { get; set; }
 }
 public static class HidInspector {
  [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }
  [StructLayout(LayoutKind.Sequential)] struct Caps {
   public ushort Usage, UsagePage, Input, Output, Feature;
   [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
   public ushort Nodes, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
  }
  [DllImport("hid.dll")] static extern void HidD_GetHidGuid(out Guid guid);
  [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
  [DllImport("setupapi.dll", SetLastError=true)] static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
  [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
  [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
  [DllImport("hid.dll", SetLastError=true)] static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
  [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
  [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, out Caps caps);
  [DllImport("hid.dll")] static extern bool HidD_GetProductString(SafeFileHandle handle, byte[] buffer, uint size);
  [DllImport("hid.dll")] static extern bool HidD_GetManufacturerString(SafeFileHandle handle, byte[] buffer, uint size);
  [DllImport("hid.dll")] static extern bool HidD_GetSerialNumberString(SafeFileHandle handle, byte[] buffer, uint size);
  // Both native capability structures are 72 bytes; only their common ReportID field is needed.
  [StructLayout(LayoutKind.Explicit, Size=72)] struct ReportCapability {
   [FieldOffset(2)] public byte ReportId;
  }
  [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int type, [Out] ReportCapability[] caps, ref ushort count, IntPtr data);
  [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int type, [Out] ReportCapability[] caps, ref ushort count, IntPtr data);
  static byte[] ReportIds(IntPtr data, int type, ushort values, ushort buttons) {
   var ids=new SortedSet<byte>();
   foreach(bool isValue in new[] { true, false }) {
    ushort count=isValue?values:buttons;
    if(count==0) continue;
    var array=new ReportCapability[count];
    int status=isValue?HidP_GetValueCaps(type,array,ref count,data):HidP_GetButtonCaps(type,array,ref count,data);
    if(status!=0x110000) throw new InvalidOperationException("Cannot read report capabilities: "+status.ToString("X"));
    if(count>array.Length) throw new InvalidOperationException("Invalid report capability count");
    for(int i=0;i<count;i++) ids.Add(array[i].ReportId);
   }
   var result=new byte[ids.Count]; ids.CopyTo(result); return result;
  }
  public static HidInfo[] Inspect(int vid, int pid) {
   Guid guid; HidD_GetHidGuid(out guid);
   IntPtr set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,18);
   if(set==new IntPtr(-1)) throw new Win32Exception();
   var result=new List<HidInfo>();
   try {
    for(uint i=0;;i++) {
     var data=new InterfaceData(); data.Size=Marshal.SizeOf(typeof(InterfaceData));
     if(!SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref data)) {
      int error=Marshal.GetLastWin32Error(); if(error==259) break; throw new Win32Exception(error);
     }
     uint required; SetupDiGetDeviceInterfaceDetail(set,ref data,IntPtr.Zero,0,out required,IntPtr.Zero);
     IntPtr detail=Marshal.AllocHGlobal((int)required);
     try {
      Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
      if(!SetupDiGetDeviceInterfaceDetail(set,ref data,detail,required,out required,IntPtr.Zero)) throw new Win32Exception();
      string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
      if(path.IndexOf(string.Format("vid_{0:x4}&pid_{1:x4}",vid,pid),StringComparison.OrdinalIgnoreCase)<0) continue;
      using(var handle=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) {
       if(handle.IsInvalid) throw new Win32Exception();
       IntPtr parsed; if(!HidD_GetPreparsedData(handle,out parsed)) throw new Win32Exception();
       try {
        Caps caps; int status=HidP_GetCaps(parsed,out caps); if(status!=0x110000) throw new Exception("HidP_GetCaps: "+status);
        byte[] product=new byte[254], manufacturer=new byte[254], serial=new byte[254];
        bool p=HidD_GetProductString(handle,product,254), m=HidD_GetManufacturerString(handle,manufacturer,254);
        bool s=HidD_GetSerialNumberString(handle,serial,254);
        result.Add(new HidInfo { Path=path, Product=p?System.Text.Encoding.Unicode.GetString(product).TrimEnd('\0'):null,
         Manufacturer=m?System.Text.Encoding.Unicode.GetString(manufacturer).TrimEnd('\0'):null,
         SerialNumber=s?System.Text.Encoding.Unicode.GetString(serial).TrimEnd('\0'):null,
         InputReportIds=ReportIds(parsed,0,caps.InputValues,caps.InputButtons),
         OutputReportIds=ReportIds(parsed,1,caps.OutputValues,caps.OutputButtons),
         FeatureReportIds=ReportIds(parsed,2,caps.FeatureValues,caps.FeatureButtons),
         UsagePage=caps.UsagePage, Usage=caps.Usage, InputReportBytes=caps.Input, OutputReportBytes=caps.Output, FeatureReportBytes=caps.Feature });
       } finally { HidD_FreePreparsedData(parsed); }
      }
     } finally { Marshal.FreeHGlobal(detail); }
    }
   } finally { SetupDiDestroyDeviceInfoList(set); }
   return result.ToArray();
  }
 }
}
'@
}
[DeskDisplay.Diagnostics.HidInspector]::Inspect($VendorId, $ProductId) | ConvertTo-Json -Depth 4
