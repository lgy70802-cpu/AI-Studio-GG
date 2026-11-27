using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    [StructLayout(LayoutKind.Sequential)] struct InputInfo { public uint Size, Tick; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref InputInfo info);
    readonly HashSet<string> standbySerials = new();
    readonly Dictionary<string, long> wakeTicks = new();
    async Task RefreshDisplayPower()
    {
        var input = new InputInfo { Size = (uint)Marshal.SizeOf<InputInfo>() };
        var idle = GetLastInputInfo(ref input) ? unchecked((uint)Environment.TickCount - input.Tick) / 1000d : 0;
        foreach (var device in saved.Values.ToArray())
        {
            var sinceWake = wakeTicks.TryGetValue(device.Serial, out var tick) ? (Environment.TickCount64 - tick) / 1000d : double.MaxValue;
            var standby = device.ManualStandby || device.StandbyMinutes > 0 && Math.Min(idle, sinceWake) >= device.StandbyMinutes * 60;
            if (standby) standbySerials.Add(device.Serial); else standbySerials.Remove(device.Serial);
            if (!outputs.TryGetValue(device.Serial, out var output)) continue;
            output.DisplayBrightness = device.Brightness; output.DisplayStandby = standby;
            if (themeSessions.TryGetValue(device.Serial, out var session))
            {
                var paused = !device.Playing || standby;
                if (paused != session.Paused) { await session.Renderer.SetPaused(paused); session.Paused = paused; }
            }
        }
    }
    async Task PowerCommand(string serial, JsonElement message)
    {
        await EndThemePreview(serial, true);
        if (!saved.TryGetValue(serial, out var device)) throw new InvalidOperationException("设备不存在。");
        var action = message.TryGetProperty("action", out var a) ? a.GetString() : null;
        if (action is "wake" or "standby" && device.ImageFile == null) throw new InvalidOperationException("请先为这台设备选择内容。");
        var brightness = message.TryGetProperty("brightness", out var level) ? level.GetInt32() : device.Brightness;
        var minutes = message.TryGetProperty("sleepMinutes", out var delay) ? delay.GetInt32() : device.StandbyMinutes;
        if (brightness is < 0 or > 100 || minutes is < 0 or > 1440) throw new InvalidOperationException("亮度或待机时间超出范围。");
        var next = device with { Brightness = brightness, StandbyMinutes = minutes,
            ManualStandby = action == "standby" || action != "wake" && device.ManualStandby };
        if (action == "wake") { wakeTicks[serial] = Environment.TickCount64; next = next with { Playing = true }; }
        saved[serial] = next;
        try
        {
            if (next.ImageFile != null && connectedSerials.Contains(serial) && !outputs.ContainsKey(serial)) await Start(next);
            await RefreshDisplayPower(); Save();
        }
        catch { saved[serial] = device; throw; }
    }
    object DesktopPreferences()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return new { closeToTray, autoStart = key?.GetValue("MiraBoxStudio.Next") is string,
            hardwareBrightness = false, hardwarePower = false, note = "亮度为软件像素调暗；待机发送黑帧，不控制背光。" };
    }
}
