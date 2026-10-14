using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Sona.App.Windows;

internal sealed class DiscordCredentials
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public double ExpiresAt { get; set; }
}

internal sealed class ProtectedCredentials(string path)
{
    public bool Exists => File.Exists(path);
    public DiscordCredentials Load()
    {
        if (!Exists || new FileInfo(path).Length > 131072) throw new IOException("Discord setup is missing or invalid.");
        var plain = Crypt(File.ReadAllBytes(path), false);
        try { return JsonSerializer.Deserialize<DiscordCredentials>(plain) ?? throw new IOException("Discord setup is invalid."); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void Save(DiscordCredentials credentials)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(credentials);
        try
        {
            var encrypted = Crypt(plain, true); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temporary, encrypted); File.Move(temporary, path, true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public void ImportHush()
    {
        if (Exists) throw new IOException("Existing Sona Discord setup was preserved.");
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hush");
        var envPath = Path.Combine(root, ".env"); var tokenPath = Path.Combine(root, "token.json");
        if (!File.Exists(envPath) || new FileInfo(envPath).Length > 65536) throw new IOException("Hush setup was not found or was too large.");
        var credentials = new DiscordCredentials();
        foreach (var line in File.ReadLines(envPath))
        {
            var split = line.IndexOf('='); if (split < 0) continue;
            var key = line[..split].Trim(); var value = line[(split + 1)..].Trim().Trim('\"', '\'');
            if (key == "DISCORD_CLIENT_ID") credentials.ClientId = value;
            else if (key == "DISCORD_CLIENT_SECRET") credentials.ClientSecret = value;
        }
        if (credentials.ClientId.Length is < 5 or > 50 || !credentials.ClientId.All(char.IsAsciiDigit) || credentials.ClientSecret.Length is < 8 or > 256) throw new IOException("Hush Discord setup was incomplete.");
        if (File.Exists(tokenPath) && new FileInfo(tokenPath).Length <= 65536)
        {
            using var token = JsonDocument.Parse(File.ReadAllText(tokenPath, Encoding.UTF8));
            credentials.AccessToken = token.RootElement.TryGetProperty("access_token", out var access) ? access.GetString() ?? "" : "";
            credentials.RefreshToken = token.RootElement.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "";
            credentials.ExpiresAt = token.RootElement.TryGetProperty("expires_at_epoch", out var expiry) ? expiry.GetDouble() : 0;
        }
        Save(credentials);
        // Credential contents remain inside this process. The legacy files are not
        // removed until the separately verified Hush retirement step.
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Size; public IntPtr Data; }
    private static byte[] Crypt(byte[] bytes, bool protect)
    {
        var input = new Blob { Size = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }; var output = new Blob();
        Marshal.Copy(bytes, 0, input.Data, bytes.Length);
        try
        {
            var success = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) throw new IOException("Windows could not protect or unlock Discord setup.");
            var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, output.Size); return result;
        }
        finally
        {
            for (var i = 0; i < input.Size; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) { for (var i = 0; i < output.Size; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
    }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
}
