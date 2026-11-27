using System.IO;
using System.Text.Json.Nodes;

namespace MiraBox.App;

internal static class StudioAtomicFile
{
    public static void Write(string path, string json)
    {
        _ = JsonNode.Parse(json) ?? throw new InvalidDataException("JSON 数据为空。");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var file = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(file)) { writer.Write(json); writer.Flush(); file.Flush(true); }
        if (File.Exists(path))
        {
            try { _ = JsonNode.Parse(File.ReadAllText(path)); File.Copy(path, path + ".previous", true); }
            catch (System.Text.Json.JsonException) { }
        }
        File.Move(path + ".tmp", path, true);
    }
    public static void Recover(string path, Func<JsonNode?, bool>? valid = null)
    {
        if (!File.Exists(path)) return;
        bool Valid(string file)
        {
            try { var node = JsonNode.Parse(File.ReadAllText(file)); return node != null && (valid?.Invoke(node) ?? true); }
            catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or FormatException) { return false; }
        }
        if (!Valid(path))
        {
            var previous = path + ".previous";
            if (!File.Exists(previous)) throw new InvalidDataException("数据损坏，原文件已保留。请从完整备份包恢复：" + Path.GetFileName(path));
            if (!Valid(previous)) throw new InvalidDataException("备份也已损坏，原文件已保留。");
            File.Copy(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            File.Copy(previous, path, true);
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(path)!, "recovery.log"), DateTimeOffset.Now + " restored " + Path.GetFileName(path) + Environment.NewLine);
        }
    }
}
