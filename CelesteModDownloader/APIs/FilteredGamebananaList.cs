using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CelesteModDownloader.Models;

namespace CelesteModDownloader.APIs;

public class FilteredByFirstFileGamebananaList : IGamebananaListAPI
{
    private readonly IGamebananaListAPI _wrapped;
    private readonly BlacklistFile _blacklistFile;
    private readonly HttpClient _client;
    private readonly string _downloadFolder;

    private readonly string _cacheFilePath;
    
    private readonly Func<GamebananaMod, GamebananaFile, IReadOnlyList<string>, bool> _predicate;

    public FilteredByFirstFileGamebananaList(IGamebananaListAPI wrapped, BlacklistFile blacklistFile, HttpClient client,
        string downloadFolder, string cacheFilePath,
        Func<GamebananaMod, GamebananaFile, IReadOnlyList<string>, bool> predicate)
    {
        _wrapped = wrapped;
        _predicate = predicate;
        _cacheFilePath = cacheFilePath;
        _downloadFolder = downloadFolder;
        _blacklistFile = blacklistFile;
        _client = client;
    }

    public async Task<IReadOnlyList<GamebananaMod>> GetAllModsAsync()
    {
        var cache = File.Exists(_cacheFilePath) ? JsonSerializer.Deserialize<CacheFile>(await File.ReadAllTextAsync(_cacheFilePath)) : null;
        cache ??= new();

        var cacheChanged = false;
        
        var mods = await _wrapped.GetAllModsAsync();
        ConcurrentBag<GamebananaMod> ret = [];

        await Parallel.ForEachAsync(mods, async (mod, token) =>
        {
            var firstFile = mod.GetLatestFile(_blacklistFile.BannedFilenames);
            if (firstFile is null)
                return;

            if (cache.KnownFileIds.TryGetValue(firstFile.FileId(), out var entry))
            {
                if (!entry.IsValid)
                    return;
            } else if (!firstFile.ExistsLocally(_downloadFolder))
            {
                var tries = 0;
                nextAttempt:
                Console.WriteLine($"Checking file list for: {firstFile.Name}");
                try
                {
                    var fileList = await firstFile.GetFileListAsync(_client);

                    if (!_predicate(mod, firstFile, fileList))
                    {
                        cache.KnownFileIds[firstFile.FileId()] = new(false);
                        cacheChanged = true;
                        return;
                    }

                    cache.KnownFileIds[firstFile.FileId()] = new(true);
                    cacheChanged = true;
                }
                catch
                {
                    if (tries++ < 3)
                    {
                        await Task.Delay(1000, token);
                        Console.WriteLine($"Retrying to get file list for: {firstFile.Name}");
                        goto nextAttempt;
                    }
                    Console.WriteLine($"Failed to get file list for: {firstFile.Name}");
                    return;
                }
            }
            
            ret.Add(mod);
        });

        if (cacheChanged)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath) ?? "");
            await File.WriteAllTextAsync(_cacheFilePath, JsonSerializer.Serialize(cache));
        }

        return ret.ToList();
    }

    public record CacheFile
    {
        [JsonPropertyName("knownInvalidFileIds")]
        public ConcurrentDictionary<long, Entry> KnownFileIds { get; set; } = [];

        public record Entry(bool IsValid);
    }
}