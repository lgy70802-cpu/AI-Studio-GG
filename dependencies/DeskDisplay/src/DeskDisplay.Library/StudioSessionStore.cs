using System.Security.Cryptography;
using System.Text;

namespace DeskDisplay.Library;

public sealed class StudioSessionStore
{
    private readonly string _path;
    public StudioSessionStore(string? path = null) => _path = path ?? Path.Combine(DeskDisplay.Core.Storage.StudioDataPaths.Root, "studio.session");
    public bool HasToken => File.Exists(_path);
    private string ScopedPath(string serverUrl) => _path + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(new Uri(serverUrl.TrimEnd('/')).AbsoluteUri))) + ".session";
    public string? Read(string serverUrl)
    {
        try
        {
            var path = ScopedPath(serverUrl);
            if (!File.Exists(path)) return null;
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
        }
        catch { return null; }
    }
    public void Save(string serverUrl, string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
        var path = ScopedPath(serverUrl);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, true);
    }
    public void Clear(string serverUrl) => File.Delete(ScopedPath(serverUrl));
}
