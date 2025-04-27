using CelesteModDownloader.APIs;
using CelesteModDownloader.Models;
using System.IO.Compression;
using System.Text.Json;
using CelesteModDownloader;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<MyCommands>();
app.Run(args);

/*
var downloadFolder = args is [var df, ..] ? df : @"testDownloads";
var mode = args is [_, var m, ..] ? m : "downloadBins";

var blacklistFile = $"{downloadFolder}/helperDownloaderBlacklist.json";
var blacklist = (File.Exists(blacklistFile) ? JsonSerializer.Deserialize<BlacklistFile>(File.ReadAllText(blacklistFile), jsonOptions) : null) ?? new(new(), new());


var downloadClient = new HttpClient();

switch (mode)
{
    case "downloadHelpers":
    {
        var updater = new ModAutoUpdater(new MaddiesGamebananaList(downloadClient, [
            5081, // helpers
            4632,  // other/misc
            575 // tools:other/misc
        ]), downloadFolder, blacklist, downloadClient);
        await updater.UpdateAllMods();
        break;
    }
    case "downloadBins":
    {
        var isMapFile = (string f) => f.StartsWith("Maps") && f.EndsWith(".bin");

        
        //var modListApi = new MaddiesGamebananaList(downloadClient, [
        //    -1,
        //]);
        var modListApi = new FileGamebananaList($"{downloadFolder}/modList.json");
        
        var modListOnlyWithBinsApi = new FilteredByFirstFileGamebananaList(modListApi, blacklist, downloadClient, downloadFolder, $"{downloadFolder}/binDownloadCache.json",
            (mod, file, fileList) => fileList.Any(isMapFile));
        
        var updater = new ModAutoUpdater(modListOnlyWithBinsApi, downloadFolder, blacklist, downloadClient);
        
        var newZips = await updater.UpdateAllMods();

        var meta = new ExtractedBinMeta();

        var binsPath = Path.Combine(downloadFolder, "_bins");
        
        Directory.Delete(binsPath, recursive: true);

        foreach (var modFileInfo in newZips)
        {
            Console.WriteLine($"Extracing {modFileInfo.Filepath}");
            using var archive = ZipFile.OpenRead(modFileInfo.Filepath);

            foreach (var entry in archive.Entries.Where(x => isMapFile(x.FullName)))
            {
                await using var entryStream = entry.Open();

                var path = Path.Combine(binsPath, $"{modFileInfo.GbMod.GameBananaId ?? 0}", entry.FullName["Maps/".Length..].Replace("..", "_."));
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "");

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                entry.ExtractToFile(path);
                
                meta.AllFiles.Add(new ExtractedBinMeta.BinFile
                {
                    Filepath = Path.GetRelativePath(binsPath, path).Replace('\\', '/'),
                    ZipEntry = entry.FullName,
                    GamebananaMod = modFileInfo.GbMod.ToSimpleMod(),
                    GamebananaFile = modFileInfo.GbFile,
                });
            }
        }
        
        File.WriteAllText(Path.Combine(binsPath, "meta.json"), JsonSerializer.Serialize(meta, jsonOptions));
        
        break;
    }
}

return;
*/

public record ExtractedBinMeta
{
    public List<BinFile> AllFiles { get; set; } = [];

    public record BinFile
    {
        public string Filepath { get; set; }
        
        public string ZipEntry { get; set; }
        
        public SimpleGamebananaMod GamebananaMod { get; set; }
        
        public GamebananaFile GamebananaFile { get; set; }
    }
}

public class MyCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Downloads and updates all helper mods with the provided GameBanana categories to the given directory.</summary>
    /// <param name="dir">-d, Directory to download mods into.</param>
    /// <param name="categories">-c, List of GameBanana category id's to download mods from.</param>
    /// <param name="modList">Specifies how to retrieve the mod list. Can be 'maddie' for Maddie480's APIs, or a filepath to a .json file.</param>
    public async Task<int> Update(string dir, int[] categories, string modList = "maddie")
    {
        using var downloadClient = new HttpClient();

        Directory.CreateDirectory(dir);
        
        var blacklistFile = $"{dir}/modDownloaderBlacklist.json";
        BlacklistFile blacklist = new(new(), new());
        if (!File.Exists(blacklistFile))
        {
            await File.WriteAllTextAsync(blacklistFile, JsonSerializer.Serialize(blacklist, JsonOptions));
        }
        else
        {
            try
            {
                var jsonString = await File.ReadAllTextAsync(blacklistFile);
                blacklist = JsonSerializer.Deserialize<BlacklistFile>(jsonString, JsonOptions) ?? throw new NullReferenceException();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading blacklist file: {ex}");
                return -2;
            }
        }

        IGamebananaListAPI api;
        switch (modList)
        {
            case "maddie":
                api = new MaddieGamebananaList(downloadClient, categories);
                break;
            default:
                if (!modList.EndsWith(".json"))
                {
                    Console.WriteLine($"Unknown mod list API: {modList}.");
                    return -3;
                }

                if (!File.Exists(modList))
                {
                    Console.WriteLine($"Mod list file '{modList}' does not exist.");
                    return -4;
                }
                
                api = new FileGamebananaList(modList, categories);
                break;
        }
        
        var updater = new ModAutoUpdater(api, dir, blacklist, downloadClient);
        
        var result = await updater.UpdateAllMods();

        if (!result.ModsFetchedSuccessfully || result.FailedMods.Count > 0)
            return -1;

        return 0;
    }
    
    /// <summary>Downloads and updates all helper mods to the given directory. Same as `download -c 5081,4632,575`</summary>
    /// <param name="dir">-d, Directory to download mods into.</param>
    /// <param name="modList">Specifies how to retrieve the mod list. Can be `maddie` for Maddie480's APIs, or a filepath to a .json file.</param>
    public Task UpdateHelpers(string dir, string modList = "maddie")
    {
        return Update(dir, [
            5081, /* helpers */
            4632, /* other/misc */
            575 /*tools:other/misc*/
        ], modList);
    }
}