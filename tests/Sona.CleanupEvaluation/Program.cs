using System.Security.Cryptography;
using System.Text.Json;
using Sona.Core;

var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
static string Hash(string path) { using var input=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(); }
static void RequireNew(string path) { if(!Path.IsPathFullyQualified(path)||File.Exists(path)||Directory.Exists(path))throw new IOException("Supply a new absolute evidence path; earlier evidence is preserved."); }
static Preferences Settings(string[] terms) => new() { CleanupEnabled=true,CleanupEngine="Auto",CleanupTone="formal",CleanupIdleMinutes=10,Terms=terms.ToList() };
if(args is ["prepare",var corpusPath,var preparedPath])
{
    RequireNew(preparedPath);
    if(!Path.IsPathFullyQualified(corpusPath)||new FileInfo(corpusPath).Length>1024*1024)throw new IOException("Supply a bounded absolute corpus path.");
    var corpus=JsonSerializer.Deserialize<Corpus>(File.ReadAllBytes(corpusPath),options)??throw new IOException("Invalid corpus.");
    if(corpus.SchemaVersion!=1||corpus.Cases.Length is <1 or >64||corpus.Terms.Length>256||corpus.Cases.Select(item=>item.Name).Distinct().Count()!=corpus.Cases.Length)throw new IOException("Invalid bounded corpus.");
    var settings=Settings(corpus.Terms);
    var rows=corpus.Cases.Select(item=> {
        if(string.IsNullOrWhiteSpace(item.Name)||item.Name.Length>80)throw new IOException("Invalid case name.");
        var request=CleanupRules.Prepare(item.Input,settings,"Codex")??throw new IOException("A synthetic case exceeds Sona's cleanup bounds: "+item.Name);
        var protectedText=request.Prompt.Split("\ntext: ",2)[1].Split("\nout:\n",2)[0];
        return new {item.Name,item.Input,item.Expectation,request.Prompt,ProtectedText=protectedText};
    }).ToArray();
    using var output=new FileStream(preparedPath,FileMode.CreateNew);
    JsonSerializer.Serialize(output,new {corpus.SchemaVersion,InputOrigin="Sona-maintained-synthetic-corpus",CorpusSha256=Hash(corpusPath),CoreModuleBuild=typeof(CleanupRules).Assembly.ManifestModule.ModuleVersionId,corpus.Terms,Cases=rows},options);
    Console.WriteLine($"Prepared {rows.Length} synthetic cases using Sona.Core.");
}
else if(args is ["restore",var measuredPath,var restoredPath])
{
    RequireNew(restoredPath);
    if(!Path.IsPathFullyQualified(measuredPath)||new FileInfo(measuredPath).Length>8*1024*1024)throw new IOException("Supply a bounded absolute measurement path.");
    using var measured=JsonDocument.Parse(File.ReadAllBytes(measuredPath));
    var root=measured.RootElement;var terms=root.GetProperty("terms").Deserialize<string[]>()??throw new IOException("Missing protected terms.");
    var samples=root.GetProperty("samples");
    if(terms.Length>256||samples.GetArrayLength() is <1 or >64)throw new IOException("Invalid bounded measurement.");
    var settings=Settings(terms);var rows=new List<object>();
    foreach(var sample in samples.EnumerateArray())
    {
        var original=sample.GetProperty("input").GetString()??throw new IOException("Missing input.");
        var truncated=sample.TryGetProperty("truncated",out var cut)&&cut.ValueKind==JsonValueKind.True;
        var variants=new List<object>();var outputs=sample.GetProperty("outputs");
        if(outputs.GetArrayLength() is <1 or >3)throw new IOException("Invalid repeat count.");
        foreach(var completion in outputs.EnumerateArray())
        {
            var raw=completion.GetString()??throw new IOException("Missing output.");
            if(original.Length>4000||raw.Length>6000)throw new IOException("Text bound exceeded.");
            var result=await CleanupProcessor.RunAsync(original,settings,"Codex",(_,_)=>truncated?throw new IOException("Truncated evaluation output."):Task.FromResult(raw),CancellationToken.None);
            variants.Add(new {Raw=raw,result.Text,result.Applied,result.Outcome});
        }
        rows.Add(new {Name=sample.GetProperty("case").GetString(),Input=original,Variants=variants});
    }
    using var output=new FileStream(restoredPath,FileMode.CreateNew);
    JsonSerializer.Serialize(output,new {MeasurementSha256=Hash(measuredPath),CoreModuleBuild=typeof(CleanupRules).Assembly.ManifestModule.ModuleVersionId,Samples=rows,Note="Preservation acceptance is not semantic correctness."},options);
    Console.WriteLine($"Replayed {rows.Count} cases through Sona's actual cleanup processor.");
}
else throw new ArgumentException("prepare ABSOLUTE_CORPUS NEW_ABSOLUTE_PREPARED or restore ABSOLUTE_MEASUREMENT NEW_ABSOLUTE_RESTORED");

internal sealed record Corpus(int SchemaVersion,string[] Terms,Case[] Cases);
internal sealed record Case(string Name,string Input,string Expectation);
