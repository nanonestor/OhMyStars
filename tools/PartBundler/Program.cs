// Generates KSA part asset XML from GLB + PNG inputs by invoking the game's
// own import pipeline (KSA.GlbImport). Because the installed game's KSA.dll
// does the serialization, the output always matches the installed game's
// current part XML schema - rerun this after game updates to stay in sync.
//
// Usage: PartBundler <inputDir> [outputDir] [gameDir]
//        PartBundler --validate <assets.xml> [...] [gameDir]
//
// The input directory is scanned for file sets sharing a stem (text before
// the first underscore), per KSA.GlbImport.PartInputSet:
//   <Stem>_MeshAtlas.glb, <Stem>_Prefab*.glb, <Stem>_*Diffuse.png,
//   <Stem>_*Normal.png, <Stem>_*PBR.png, ...
// Output: <Stem>Assets.xml (plus Meshes/<Stem>_CollisionAtlas.glb when the
// prefab uses convex hull colliders).
//
// --validate deserializes asset XML files with the game's serializer and
// reports any element or attribute the game would silently ignore.

using System.Runtime.CompilerServices;
using System.Runtime.Loader;

internal static class Program
{
    private static string _gameDir = string.Empty;

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: PartBundler <inputDir> [outputDir] [gameDir]");
            Console.Error.WriteLine("       PartBundler --validate <assets.xml> [...] [gameDir]");
            return 1;
        }
        bool validate = args[0] == "--validate";
        string[] rest = validate ? args[1..] : args;
        string lastArg = rest.Length > 0 ? rest[^1] : string.Empty;
        bool lastIsGameDir = rest.Length > (validate ? 1 : 2)
                             && File.Exists(Path.Combine(lastArg, "KSA.dll"));
        _gameDir = lastIsGameDir
            ? lastArg
            : Environment.GetEnvironmentVariable("KSAGameDir")
              ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                              "Ahwoo", "Games", "Kitten Space Agency");
        if (lastIsGameDir)
        {
            rest = rest[..^1];
        }

        if (!File.Exists(Path.Combine(_gameDir, "KSA.dll")))
        {
            Console.Error.WriteLine($"KSA.dll not found in '{_gameDir}'. Pass the game directory as the last argument or set KSAGameDir.");
            return 1;
        }

        // KSA.dll and its dependencies live in the game directory, not next
        // to this tool - resolve them from there at load time.
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string candidate = Path.Combine(_gameDir, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        if (validate)
        {
            return Validate(rest);
        }

        string input = Path.GetFullPath(rest[0]);
        string output = Path.GetFullPath(rest.Length > 1 ? rest[1] : rest[0]);
        if (!Directory.Exists(input))
        {
            Console.Error.WriteLine($"Input directory not found: {input}");
            return 1;
        }
        return Run(input, output);
    }

    // No KSA types may leak into Main: the resolver must be registered
    // before the runtime tries to load KSA.dll.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string input, string output)
    {
        Directory.CreateDirectory(output);
        int count = 0;
        foreach (var set in KSA.GlbImport.PartInputSet.FromDirectory(new DirectoryInfo(input)))
        {
            if (set.MeshAtlasGlb == null && set.PrefabGlbs.Count == 0)
            {
                continue;
            }
            Console.WriteLine($"Bundling '{set.Stem}'...");
            var bundle = KSA.GlbImport.PartAssetBundler.Build(set.Stem + ": ", set, output);
            string xmlPath = Path.Combine(output, set.Stem + "Assets.xml");
            KSA.GlbImport.ToolXml.Write(bundle, xmlPath);

            // The dev pipeline converts source PNGs to KTX2 and the bundler
            // assumes that; this mod ships the PNGs directly.
            File.WriteAllText(xmlPath, File.ReadAllText(xmlPath).Replace(".ktx2", ".png"));
            Console.WriteLine($"  wrote {xmlPath}");
            count++;
        }
        if (count == 0)
        {
            Console.Error.WriteLine("No part input sets (GLB/PNG files sharing a name stem) found.");
            return 1;
        }
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Validate(string[] paths)
    {
        if (paths.Length == 0)
        {
            Console.Error.WriteLine("No XML files given to validate.");
            return 1;
        }
        var serializer = new System.Xml.Serialization.XmlSerializer(
            typeof(KSA.AssetBundle), KSA.XmlHelper.AttributeOverrides);
        int problems = 0;
        serializer.UnknownElement += (_, e) =>
        {
            problems++;
            Console.WriteLine($"  [UNKNOWN ELEMENT] line {e.LineNumber}: <{e.Element.Name}> (inside {e.ObjectBeingDeserialized?.GetType().Name})");
        };
        serializer.UnknownAttribute += (_, e) =>
        {
            if (e.Attr.Name == "Modify")
            {
                return; // handled by the game's patching layer, not the serializer
            }
            problems++;
            Console.WriteLine($"  [UNKNOWN ATTRIBUTE] line {e.LineNumber}: {e.Attr.Name} (inside {e.ObjectBeingDeserialized?.GetType().Name})");
        };
        foreach (string path in paths)
        {
            Console.WriteLine($"Validating {path}...");
            try
            {
                using var reader = File.OpenRead(path);
                var bundle = (KSA.AssetBundle?)serializer.Deserialize(reader);
                Console.WriteLine($"  ok: {bundle?.Assets.Count ?? 0} asset(s)");
            }
            catch (Exception ex)
            {
                problems++;
                Console.Error.WriteLine($"  [ERROR] {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        return problems == 0 ? 0 : 2;
    }
}
