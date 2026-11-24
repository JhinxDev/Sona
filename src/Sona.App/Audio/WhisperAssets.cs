using System.Security.Cryptography;

namespace Sona.App.Audio;

internal static class WhisperAssets
{
    private sealed record PinnedFile(long Bytes, string Sha256, bool Required);

    private static readonly IReadOnlyDictionary<string, PinnedFile> CpuFiles = new Dictionary<string, PinnedFile>(StringComparer.OrdinalIgnoreCase)
    {
        ["whisper-server.exe"] = new(726016, "a97456e747f28e7bb09c1dac35f40d94404c5ecffa74aaaaa7a3753ea6543121", true),
        ["whisper.dll"] = new(1370112, "034396d75bb5720c1851674ff64a96192b58f6927b68d334ebd254ce6cad9674", true),
        ["ggml.dll"] = new(62464, "31fe18616d872af9b6134b47a6dff8072ae4ecbe5df0273f4f8dc36766c9db8e", true),
        ["ggml-base.dll"] = new(693248, "bede0d6b2d387236ecd2b5dd676d51c8984b5b6e0ae461a12174e9c01345c5f0", true),
        ["SDL2.dll"] = new(2500096, "de23db1694a3c7a4a735e7ecd3d214b2023cc2267922c6c35d30c7fc7370d677", true),
        ["llama.dll"] = new(2755072, "74b7857b05951c0dff4c7baf796205433c84ee40c9bbb0bda3f182d4425ce339", false),
        ["parakeet.dll"] = new(582144, "abe7399d64a67b1ebcb8d33becd07c67cbf186cb0d2bab34a4264feefdc337c4", false),
        ["ggml-cpu-alderlake.dll"] = new(1009664, "d8f5c56ea490394eeae8b271650f2647aecd039a542d6df9e0140726e62a4603", false),
        ["ggml-cpu-cannonlake.dll"] = new(1049600, "74127b40abd4f595d9db6af0fa313a02e18ff42fc228bfa05281e7c788dd4507", false),
        ["ggml-cpu-cascadelake.dll"] = new(1061376, "4b11c427b844eeac87529af9a8d94b35aad2024666beb2a35e59fe3077e4b81e", false),
        ["ggml-cpu-haswell.dll"] = new(1010688, "1306b1014c8118d27234d8ddc0e66ced1f8c8476bf85071595fd182ea9b7263b", false),
        ["ggml-cpu-icelake.dll"] = new(1061376, "2b4dc4c66e7d8fe2807283816e9cf3b7f7d0556f59df34069871e37eab8aed40", false),
        ["ggml-cpu-sandybridge.dll"] = new(1026560, "04edbdebc01a1d421062a960881a4f34d520d99693999518243f332406444c6e", false),
        ["ggml-cpu-skylakex.dll"] = new(1049600, "1774714cf93621406ff35344b54715619a83242ae3e331e44d98a3b35ce801be", false),
        ["ggml-cpu-sse42.dll"] = new(862720, "f060318772c4c2dbac312bbfff6ea700b85dbc502bb38585a7186d06d549301a", false),
        ["ggml-cpu-x64.dll"] = new(866816, "4b4b05caba40d4309b5d710ef74c59ba190516b8489f27bdb1ba11ae33c6e9d3", false)
    };

    private static readonly IReadOnlyDictionary<string, PinnedFile> CudaFiles = new Dictionary<string, PinnedFile>(StringComparer.OrdinalIgnoreCase)
    {
        ["whisper-server.exe"] = new(728576, "99135fa0e14059e026eef112da991bdd7ba9b22b01259b31fb58446ca6d245e4", true),
        ["whisper.dll"] = new(1312256, "d8eb92588ee9aa850150703828ce6890dee4a9991cd68c8da1ae1e162a089742", true),
        ["ggml.dll"] = new(60928, "42520bbdd998be8efffc358c108c7dbdae79b78bb0ec2dd8ab96b1428b537a13", true),
        ["ggml-base.dll"] = new(677376, "d4c84248be354504b3ec9bc09b2cfcc58d1c310e5c3bee6a0409a0d7ed699714", true),
        ["ggml-cuda.dll"] = new(571467776, "cd8686d659f3d86130a7ef4b397b2aac57c50783c4af6a6cc816ccb139e2a51e", true),
        ["cublas64_11.dll"] = new(88668160, "8ca516b96b29c2fba2344909a896bc1cd7951f6cd11fe595a8a3929c02cccbed", true),
        ["cublasLt64_11.dll"] = new(544070144, "3d06ca4e4893adb7a153ecd23a540e92817c967312b44646d8c3f91b089196e6", true),
        ["cudart64_110.dll"] = new(526848, "ba5c2fb526c4ee4bb218ceb3fa5e8bfde89ce474f38711fdcce802549bf9fc6f", true),
        ["nvrtc64_112_0.dll"] = new(40629248, "7a1d6c894d9db5f043e7cca5ea328bc4f29c45aa7af3f0dd1bde1f31f02b41bd", true),
        ["nvrtc-builtins64_118.dll"] = new(7909888, "8642cd940445f0eb79ae73c04b3dfb8c8e61fc3aca191dc2239b28a9cb6964d8", true),
        ["SDL2.dll"] = new(2500096, "de23db1694a3c7a4a735e7ecd3d214b2023cc2267922c6c35d30c7fc7370d677", true),
        ["cudart32_110.dll"] = new(467456, "b8bfc244dd0916ddf7b45e39c101f165a0d9f4846616eaf34336a2c374409408", false),
        ["cuinj64_118.dll"] = new(1317376, "b97f3becdfa7d5652b456377ced74766f5e21cb52d2d6edac5bc6114d786f040", false),
        ["llama.dll"] = new(2755072, "2424c416b75d9dc4ef0ff99c47f12f9e14f9f01c05703a2e67489fa02a0685ab", false),
        ["parakeet.dll"] = new(571904, "e99f11d1d6e5fac40c312b6c1cb41dec47b594128cca661314d3df19bfea1d1d", false),
        ["ggml-cpu-alderlake.dll"] = new(1004544, "03f9bedce35659705fa9190b43b9f5b0ee3f03c64381ad6d93d2f502f7fcd246", false),
        ["ggml-cpu-cannonlake.dll"] = new(1047040, "9b8380a17728eb917314268278296136de1c33419eb9cb3714ec80ecbd194535", false),
        ["ggml-cpu-cascadelake.dll"] = new(1045504, "abea9a8e91e56cc71e6d71d430336dd091b21933f3f0c796dd4b1a12d4ac84a6", false),
        ["ggml-cpu-haswell.dll"] = new(1005568, "2ebc77e22e2d75968a0122a4c967abbdf0bc8a822da31882dd5e635a9e08cb21", false),
        ["ggml-cpu-icelake.dll"] = new(1045504, "0ca38623160bab59473f497f32d05c58e18300d9b4680d8996ac6d0adcb5a93e", false),
        ["ggml-cpu-sandybridge.dll"] = new(1001472, "b9d3f59381fd7a742edea83ec95ce1a83e7648561e79857e7212a9281897cfc9", false),
        ["ggml-cpu-skylakex.dll"] = new(1047040, "9c87d468d758400e53f4efcb0e71ffcee98a7beffe5b588839d9bc7e60508b2b", false),
        ["ggml-cpu-sse42.dll"] = new(838656, "7a7bcf3a60e18a100c5eb23b90a4e4c49f388a73620195d0ffe8eb6363b87504", false),
        ["ggml-cpu-x64.dll"] = new(839168, "dedae238216ef7094e7f2cab1a08c4804f2efb0c551ca70e5f8d24d75c3b3044", false)
    };

    public static string RuntimePath(bool cuda) => Path.Combine(Transcriber.RuntimeRoot, cuda ? "whisper-cuda" : "whisper", "Release");

    public static Task<bool> VerifyAsync(bool cuda, CancellationToken token = default) => VerifyAtAsync(RuntimePath(cuda), cuda, token);

    internal static async Task<bool> VerifyAtAsync(string directory, bool cuda, CancellationToken token = default)
    {
        if (!Directory.Exists(directory) || HasReparsePoint(directory)) return false;
        var table = cuda ? CudaFiles : CpuFiles;
        foreach (var pair in table)
        {
            if (!pair.Value.Required) continue;
            var path = Path.Combine(directory, pair.Key);
            if (!await MatchesAsync(path, pair.Value, token).ConfigureAwait(false)) return false;
        }
        var cpuBackendCount = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);
            if (!table.TryGetValue(name, out var pinned)) return false;
            if (!pinned.Required && !await MatchesAsync(path, pinned, token).ConfigureAwait(false)) return false;
            if (name.StartsWith("ggml-cpu-", StringComparison.OrdinalIgnoreCase)) cpuBackendCount++;
        }
        return cpuBackendCount > 0;
    }

    private static async Task<bool> MatchesAsync(string path, PinnedFile expected, CancellationToken token)
    {
        if (!File.Exists(path) || HasReparsePoint(path)) return false;
        var info = new FileInfo(path);
        if (info.Length != expected.Bytes) return false;
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).ToLowerInvariant();
        return actual.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasReparsePoint(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        return false;
    }
}
