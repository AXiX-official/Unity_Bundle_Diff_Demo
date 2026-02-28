using BundleDiff.Runtime;

namespace PatchDemo;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: PatchDemo <baseDir> <patchDir> <outputDir>");
            Console.WriteLine("  baseDir:   current version bundle directory");
            Console.WriteLine("  patchDir:  patch package directory (contains manifest.json and patches/)");
            Console.WriteLine("  outputDir: output directory for patched bundles");
            Console.WriteLine();
            Console.WriteLine("Example: PatchDemo ./bundles_v1 ./patch_output ./bundles_v2_patched");
            return;
        }

        var baseDir = args[0];
        var patchDir = args[1];
        var outputDir = args[2];
        var manifestPath = Path.Combine(patchDir, "manifest.json");

        if (!File.Exists(manifestPath))
        {
            Console.WriteLine($"Error: manifest.json not found at {manifestPath}");
            return;
        }

        Console.WriteLine($"Base dir:     {baseDir}");
        Console.WriteLine($"Patch dir:    {patchDir}");
        Console.WriteLine($"Output dir:   {outputDir}");
        Console.WriteLine();

        var applier = new PatchApplier(baseDir, patchDir, outputDir);
        await applier.ApplyAsync(manifestPath);

        Console.WriteLine();
        Console.WriteLine("Done.");
    }
}
