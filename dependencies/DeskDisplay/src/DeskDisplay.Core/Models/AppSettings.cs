namespace DeskDisplay.Core.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string LastScene { get; set; } = "时钟";
    public string? LastVideoPath { get; set; }
    public int VideoFps { get; set; } = 30;
    public bool AutoDisplayOnStartup { get; set; }
    public string? LastImagePath { get; set; }
    public ImageDisplayMode ImageMode { get; set; } = ImageDisplayMode.Fill;
    public string? DashboardImagePath { get; set; }
    public DashboardLayout DashboardLayout { get; set; } = DashboardLayout.Split;
    public bool DashboardShowPerformance { get; set; } = true;
    // Read legacy Alpha settings. New writes use TargetDisplay and omit this field.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetDisplayId { get; set; }
    public TargetDisplaySettings? TargetDisplay { get; set; }
    public UsbOutputSettings? UsbOutput { get; set; }
    public Dictionary<string, UsbVideoPreferences> UsbVideoPreferences { get; set; } = new(StringComparer.Ordinal);
    public bool StockPluginEnabled { get; set; } = true;
    public string StockPluginSymbols { get; set; } = "600519,300750,600036,688981";
    public bool RetroCameraPluginEnabled { get; set; } = true;
    public bool RetroCameraAnimate { get; set; } = true;
    public bool RetroCameraHour12 { get; set; }
    public int RetroCameraDurationMs { get; set; } = 440;
    public int RetroCameraCropY { get; set; } = 145;
    public string StudioServerUrl { get; set; } = "http://127.0.0.1:3000";
    public bool HomePortrait { get; set; }
    public bool CloseToTray { get; set; }
    public bool LaunchAtLogin { get; set; }
    public bool RestoreD92OnStartup { get; set; }
    public List<D92ResumeContent> D92ResumeContents { get; set; } = [];
}

public sealed record D92ResumeContent(string Serial,string Kind,string? Path=null,Guid? ThemeId=null,ImageDisplayMode ImageMode=ImageDisplayMode.Uniform,Guid? LastAssetId=null,bool Paused=false);
