using System.Text.Json;
using CelesteModDownloader.Models;

namespace CelesteModDownloader.APIs;

public class FileGamebananaList(string path, int[] categories) : IGamebananaListAPI
{
    public Task<IReadOnlyList<GamebananaMod>> GetAllModsAsync()
    {
        if (File.Exists(path))
        {
            var file = File.ReadAllText(path);
            var mods = JsonSerializer.Deserialize<List<GamebananaMod>>(file) ?? [];
            var filteredMods = mods.Where(m => categories.Contains(m.CategoryId ?? 0)).ToList();
            
            return Task.FromResult<IReadOnlyList<GamebananaMod>>(filteredMods);
        }
        
        return Task.FromResult<IReadOnlyList<GamebananaMod>>([]);
    }
}