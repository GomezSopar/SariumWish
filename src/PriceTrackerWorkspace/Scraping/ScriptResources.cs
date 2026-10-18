using System.Collections.Concurrent;

namespace PriceTrackerWorkspace.Scraping;

internal static class ScriptResources
{
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    public static string DetectPrice => Load("detect-price.js");

    public static string PrepareSnapshot => Load("prepare-snapshot.js");

    public static string Inspector => Load("element-inspector.js");

    public static string InspectorStyles => Load("element-inspector.css");

    private static string Load(string fileName) => Cache.GetOrAdd(fileName, static name =>
    {
        var assembly = typeof(ScriptResources).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Scripts.{name}")
            ?? throw new InvalidOperationException($"Embedded script '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
