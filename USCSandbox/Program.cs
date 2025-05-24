using AssetsTools.NET;
using AssetsTools.NET.Extra;
using USCSandbox.Processor;
using UnityVersion = AssetRipper.Primitives.UnityVersion;

namespace USCSandbox
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hi welcome 2 USCSandbox, original program by nesrak1, fork by maybekoi/KoishiGH\n");
            var bundlePath = args[0];
            var assetsFileName = args[1];
            var shaderPathId = -1L; 
            if (args.Length > 2)
                shaderPathId = long.Parse(args[2]);
            var shaderPlatform = args.Length > 3 ? Enum.Parse<GPUPlatform>(args[3]) : GPUPlatform.d3d11;

            AssetsManager manager = new AssetsManager();
            AssetsFileInstance afileInst;
            AssetsFile afile;
            UnityVersion ver;
            int shaderTypeId;
            Dictionary<long, string> files = [];
            if (bundlePath != "null")
            {
                Console.WriteLine($"Loading bundle: {bundlePath}");
                var bundleFile = manager.LoadBundleFile(bundlePath, true);
                if (bundleFile == null)
                {
                    Console.WriteLine("Failed to load bundle file");
                    return;
                }
                Console.WriteLine($"Bundle loaded successfully. Unity version: {bundleFile.file.Header.EngineVersion}");
                
                Console.WriteLine("Available assets files in bundle:");
                foreach (var asset in bundleFile.file.GetAllFileNames())
                {
                    Console.WriteLine($"- {asset}");
                }
                
                Console.WriteLine($"Looking for assets file: {assetsFileName}");
                afileInst = manager.LoadAssetsFileFromBundle(bundleFile, assetsFileName);
                if (afileInst == null)
                {
                    Console.WriteLine("Failed to load assets file from bundle");
                    return;
                }
                Console.WriteLine("Assets file loaded successfully");
                afile = afileInst.file;

                if (!File.Exists("classdata.tpk"))
                {
                    Console.WriteLine("Error: classdata.tpk file not found in the current directory");
                    return;
                }

                ver = UnityVersion.Parse(bundleFile.file.Header.EngineVersion);
                Console.WriteLine($"Unity version: {ver}");

                Console.WriteLine("Loading class package...");
                manager.LoadClassPackage("classdata.tpk");
                
                Console.WriteLine($"Loading class database for Unity version: {ver}");
                try 
                {
                    manager.LoadClassDatabaseFromPackage(bundleFile.file.Header.EngineVersion);
                    var classDb = manager.ClassPackage.GetClassDatabase(ver.ToString());
                    if (classDb == null)
                    {
                        Console.WriteLine($"Error: Could not find class database for Unity version {ver}");
                        return;
                    }
                    var shaderClass = classDb.FindAssetClassByName("Shader");
                    if (shaderClass == null)
                    {
                        Console.WriteLine("Error: Could not find Shader class in database");
                        return;
                    }
                    shaderTypeId = shaderClass.ClassId;
                    Console.WriteLine($"Successfully loaded class database. Shader type ID: {shaderTypeId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading class database: {ex.Message}");
                    return;
                }
                
                try
                {
                    Console.WriteLine("Attempting to load globalgamemanagers...");
                    var ggm = manager.LoadAssetsFileFromBundle(bundleFile, "globalgamemanagers");
                    if (ggm != null)
                    {
                        var rsrcInfo = ggm.file.GetAssetsOfType(AssetClassID.ResourceManager)[0];
                        var rsrcBf = manager.GetBaseField(ggm, rsrcInfo);
                        var m_Container = rsrcBf["m_Container.Array"];
                        foreach (var data in m_Container.Children)
                        {
                            var name = data[0].AsString;
                            var pathId = data[1]["m_PathID"].AsLong;
                            files[pathId] = name;
                        }
                        Console.WriteLine("Successfully loaded globalgamemanagers");
                    }
                    else
                    {
                        Console.WriteLine("No globalgamemanagers found in bundle - this is normal for some bundles");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Note: Could not load globalgamemanagers: {ex.Message}");
                }
            }
            else
            {
                afileInst = manager.LoadAssetsFile(assetsFileName);
                afile = afileInst.file;
                manager.LoadClassPackage("classdata.tpk");
                manager.LoadClassDatabaseFromPackage(afile.Metadata.UnityVersion);
                ver = UnityVersion.Parse(afile.Metadata.UnityVersion);
                shaderTypeId = manager.ClassPackage.GetClassDatabase(ver.ToString()).FindAssetClassByName("Shader").ClassId;
            }

            var shaders = afileInst.file.GetAssetsOfType(shaderTypeId);
            foreach (var shader in shaders)
            {
                if (shaderPathId != -1 && shader.PathId != shaderPathId)
                    continue;
                
                var shaderBf = manager.GetExtAsset(afileInst, 0, shader.PathId).baseField;
                if (shaderBf == null)
                {
                    Console.WriteLine("Shader asset not found.");
                    return;
                }
                var shaderProcessor = new ShaderProcessor(shaderBf, ver, shaderPlatform);
                bool fileNameExists = files.TryGetValue(shader.PathId, out string? name);
                string shaderText = shaderProcessor.Process();
                if (fileNameExists)
                {
                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "out", Path.GetDirectoryName(name)!));
                    File.WriteAllText($"{Path.Combine(Environment.CurrentDirectory, "out", name)}.shader", shaderText);
                    Console.WriteLine($"{name} decompiled");
                }
                else
                {
                    Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "out", "builtin"));
                    var shaderName = shaderBf["m_ParsedForm"]["m_Name"].AsString;
                    File.WriteAllText($"{Path.Combine(Environment.CurrentDirectory, "out", "builtin", $"{shaderName.Replace('/', '_')}")}.shader", shaderText);
                    Console.WriteLine($"builtin {shaderName} decompiled");
                }
            }
        }
    }
}