using BundleDiff.Editor;

namespace DiffDemo;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: DiffDemo <oldDir> <newDir> <outputDir> [version] [baseVersion]");
            Console.WriteLine("Example: DiffDemo ./bundles_v1 ./bundles_v2 ./patch_output 2.0.0 1.0.0");
            return;
        }

        var oldDir = args[0];
        var newDir = args[1];
        var outputDir = args[2];
        var version = args.Length > 3 ? args[3] : "1.0.0";
        var baseVersion = args.Length > 4 ? args[4] : "0.0.0";

        Console.WriteLine($"Old dir:     {oldDir}");
        Console.WriteLine($"New dir:     {newDir}");
        Console.WriteLine($"Output dir:  {outputDir}");
        Console.WriteLine($"Version:     {baseVersion} -> {version}");
        Console.WriteLine();

        var generator = new DiffGenerator(oldDir, newDir, outputDir);
        var manifest = await generator.GenerateAsync(version, baseVersion);

        Console.WriteLine();
        Console.WriteLine($"Done. {manifest.Operations.Count} operation(s) generated.");
        foreach (var op in manifest.Operations)
        {
            Console.WriteLine($"  [{op.Type}] {op.BundlePath} / {op.InternalPath}");
        }
    }
}
