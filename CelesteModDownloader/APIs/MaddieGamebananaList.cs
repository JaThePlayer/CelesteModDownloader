using CelesteModDownloader.Models;
using System.Text.Json;

namespace CelesteModDownloader.APIs;

internal sealed class MaddieGamebananaList(HttpClient client, int[] categories) : IGamebananaListAPI
{
    private string GetUrl(int category, int page) => category switch
    {
        -1 => $"https://maddie480.ovh/celeste/gamebanana-list?sort=latest&page={page}",
        _ => $"https://maddie480.ovh/celeste/gamebanana-list?sort=latest&category={category}&page={page}",
    };

    public async Task<IReadOnlyList<GamebananaMod>> GetAllModsAsync()
    {
        var allMods = new List<GamebananaMod>();
        foreach (var category in categories)
        {
            int page = 1;
            while (true)
            {
                var modsThisPage = await GetModsFromPageAndCategoryAsync(page, category);

                if (modsThisPage is null or [])
                    break;

                allMods.AddRange(modsThisPage);
                page++;
            }
        }

        return allMods;
    }

    public async Task<IReadOnlyList<GamebananaMod>?> GetModsFromPageAndCategoryAsync(int page, int category)
    {
        var url = GetUrl(category, page);
        await using var apiStream = await client.GetStreamAsync(url);
        var modsThisPage = await JsonSerializer.DeserializeAsync<List<GamebananaMod>>(apiStream);

        return modsThisPage;
    }
}
