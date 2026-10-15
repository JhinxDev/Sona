using System.Reflection;
namespace Sona.App;
public static class AppVersion
{
    public static string Number { get; } = typeof(AppVersion).Assembly.GetName().Version?.ToString(3) ?? "unknown";
    public static string Short => "v" + Number;
    public static string BuildId => typeof(AppVersion).Module.ModuleVersionId.ToString("N")[..12];
    public static string Display => "Sona " + Number;
}
