using CelesteModDownloader.Models;

namespace CelesteModDownloader.APIs;

public interface IGamebananaListAPI
{
    public Task<IReadOnlyList<GamebananaMod>> GetAllModsAsync();
}
