using System.Text.Json;
using CelesteModDownloader.APIs;
using CelesteModDownloader.Models;

namespace CelesteModDownloader;

public class ModAutoUpdater
{
    private readonly IGamebananaListAPI _modListApi;

    private readonly string _downloadFolder;

    private readonly BlacklistFile _blacklist;

    private readonly HttpClient _downloadClient;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public ModAutoUpdater(IGamebananaListAPI modListApi, string downloadFolder, BlacklistFile blacklistFile, HttpClient downloadClient)
    {
        _modListApi = modListApi;
        _downloadFolder = downloadFolder;
        _blacklist = blacklistFile;
        _downloadClient = downloadClient;
    }
    
    public async Task<ModUpdateResult> UpdateAllMods()
    {
        Directory.CreateDirectory(_downloadFolder);
        
        var mods = await _modListApi.GetAllModsAsync();
        
        if (mods is not { Count: > 0 })
        {
            Console.WriteLine("Failed to fetch mods");
            return new()
            {
                ModsFetchedSuccessfully = false,
            };
        }
        Console.WriteLine($"Found {mods.Count} mods");
        
        var modListJsonFile = $"{_downloadFolder}/modList.json";

        await File.WriteAllTextAsync(modListJsonFile, JsonSerializer.Serialize(mods, JsonOptions));


        var downloadedZips = new HashSet<string>();
        var ret = new List<UpdatedModInfo>();
        
        var failedDownloads = new List<GamebananaMod>();
        
        foreach (var mod in mods)
        {
            if (_blacklist.BannedMods.Contains(mod.GameBananaId ?? -1))
            {
                //Console.WriteLine($"Mod {mod.Name} [{mod.GameBananaId}] is banned, skipping...");
                continue;
            }

            if (mod.GetLatestFile(_blacklist.BannedFilenames) is not { } file)
                continue;

            if (await file.DownloadToAsync(_downloadClient, _downloadFolder) is not { } fl)
            {
                failedDownloads.Add(mod);
                continue;
            }
            
            downloadedZips.Add(fl.FullName);
            ret.Add(new UpdatedModInfo
            {
                Filepath = fl.FullName,
                GbFile = file,
                GbMod = mod,
            });
        }

        Console.WriteLine();
        if (failedDownloads.Count > 0)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to download {failedDownloads.Count} mods:");
            foreach (var mod in failedDownloads)
            {
                Console.WriteLine($"- {mod.Name} [{mod.GameBananaId}] (from: {mod.GetLatestFile(_blacklist.BannedFilenames)?.Url ?? "<unknown URL>"})");
            }
            Console.ForegroundColor = prevColor;
        }

        // Clean up any old mod zips
        Console.WriteLine();
        Console.WriteLine("Cleaning up old zips...");
        foreach (var zipPath in Directory.EnumerateFiles(_downloadFolder, "*.zip"))
        {
            var zipFile = new FileInfo(zipPath);

            if (!downloadedZips.Contains(zipFile.FullName))
            {
                Console.WriteLine($"Deleting {zipFile.FullName}");
                zipFile.Delete();
            }
        }

        Console.WriteLine("Finished!");

        return new ModUpdateResult
        {
            ModsFetchedSuccessfully = true,
            UpdatedMods = ret,
            FailedMods = failedDownloads,
        };
    }
}

public record ModUpdateResult
{
    public required bool ModsFetchedSuccessfully { get; init; }
    
    public List<UpdatedModInfo> UpdatedMods { get; init; } = [];
    
    public List<GamebananaMod> FailedMods { get; init; } = [];
}

public record UpdatedModInfo
{
    public string Filepath { get; set; }
    
    public GamebananaFile GbFile { get; set; }
    
    public GamebananaMod GbMod { get; set; }
}