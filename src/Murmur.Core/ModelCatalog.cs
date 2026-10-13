namespace Murmur.Core;

public sealed record LocalModel(string Id, string Name, string File, long Bytes, string Sha256, int ParametersMillions, string Description, string Measurements)
{
    public string Provider { get; init; } = "OpenAI";
    public string Languages { get; init; } = "Multilingual";
    public string Runtime { get; init; } = "whisper.cpp";
    public string Repository { get; init; } = "ggerganov/whisper.cpp";
    public string Revision { get; init; } = ModelCatalog.Revision;
    public string License { get; init; } = "MIT";
    public bool IsParakeet => Runtime == "NeMo-Speech.cpp";
    public string DownloadUrl => $"https://huggingface.co/{Repository}/resolve/{Revision}/{File}";
    public string SizeLabel => $"{Bytes / 1048576d:0.0} MiB";
}

public static class ModelCatalog
{
    public const string Revision = "5359861c739e955e79d9a303bcbc70fb988958b1";
    public static IReadOnlyList<LocalModel> All { get; } = Array.AsReadOnly(new[]
    {
        new LocalModel("tiny", "Whisper Tiny", "ggml-tiny-q5_1.bin", 32152673, "818710568da3ca15689e31a743197b520007872ff9576237bda97bd1b469c3d7", 39, "Smallest download. Useful for trying local dictation on a modest CPU.", "Not measured in Sona. Smaller models can miss names and difficult speech."),
        new LocalModel("base", "Whisper Base", "ggml-base-q5_1.bin", 59707625, "422f1ae452ade6f30a004d7e5c6a43195e4433bc370bf23fac9cc591f01a8898", 74, "A compact starting point for CPU transcription.", "Not measured in Sona. Compare on your own recordings before choosing."),
        new LocalModel("small", "Whisper Small", "ggml-small-q5_1.bin", 190085487, "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb", 244, "Balanced local CPU option. Also works with an installed CUDA engine.", "Measured CPU: French 16.38% WER / 3.27 s; English 8.97% / 3.24 s. Eight clips per language."),
        new LocalModel("medium", "Whisper Medium", "ggml-medium-q5_0.bin", 539212467, "19fea4b380c3a618ec4723c3eef2eb785ffba0d0538cf43f8f235e7b3b34220f", 769, "Larger multilingual model. A CUDA engine is required by Sona for this model.", "Not measured in Sona. Runtime memory may exceed the bounded worker budget."),
        new LocalModel("turbo", "Whisper Large v3 Turbo", "ggml-large-v3-turbo-q5_0.bin", 574041195, "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2", 809, "Faster large model. A CUDA engine is required by Sona for this model.", "RTX 4080 SUPER: English 5.77% WER; 144 ms median warm request. Eight distinct clips, three timing runs each. About 1 GiB device-wide loaded VRAM delta; 1.86 GiB peak host commit. The pinned CUDA server failed a mixed-language sequence. See qualification details."),
        new LocalModel("large-v3", "Whisper Large v3", "ggml-large-v3-q5_0.bin", 1081140203, "d75795ecff3f83b5faa89d1900604ad8c780abd5739fae406de19f23ecd98ad1", 1550, "Full large multilingual model. A CUDA engine is required by Sona for this model.", "Not measured in Sona. Runtime memory may exceed the bounded worker budget."),
        new LocalModel("parakeet-v3", "Parakeet v3", "parakeet-tdt-0.6b-v3.q8_0.gguf", 713975456, "e3880d0aaaaf2c308ea2c35016b2b895c423eb3fda924c1b463d1c19b7f4d32e", 600, "NVIDIA speech recognition with punctuation and capitalization. Local CPU or CUDA transcription in 25 European languages, including English. Longer recordings use bounded local segments.", "RTX 4080 SUPER: English 5.13% WER; 21 ms median warm request. Eight distinct clips, three timing runs each. About 1 GiB device-wide loaded VRAM delta; 2.23 GiB peak host commit. French 5.60% WER / 22 ms on eight clips. Small test set; personal voice accuracy is not established.")
        { Provider = "NVIDIA", Languages = "25 European languages", Runtime = "NeMo-Speech.cpp", Repository = "nvidia/parakeet-tdt-0.6b-v3", Revision = "541d1f99c6b0c3cd0b11a95167540bb8edefd82b", License = "CC-BY-4.0" }
    });
    public static LocalModel Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? throw new ArgumentException("Unknown local model ID.");
    public static bool NeedsCuda(string id) => id is "medium" or "turbo" or "large-v3";
}
