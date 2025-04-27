using System.Globalization;
using System.Text.Json.Serialization;

namespace CelesteModDownloader.Models;

public record GamebananaFile(
    [property: JsonPropertyName("Description")] string Description,
    [property: JsonPropertyName("HasEverestYaml")] bool? HasEverestYaml,
    [property: JsonPropertyName("Size")] int? Size,
    [property: JsonPropertyName("CreatedDate")] int? CreatedDate,
    [property: JsonPropertyName("Downloads")] int? Downloads,
    [property: JsonPropertyName("URL")] string Url,
    [property: JsonPropertyName("Name")] string Name
)
{
    public long FileId()
    {
        var urlSpan = Url.AsSpan();
        return urlSpan.StartsWith("https://gamebanana.com/dl/") 
            ? long.Parse(urlSpan["https://gamebanana.com/dl/".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture) : -1;
    }

    public async Task<IReadOnlyList<string>> GetFileListAsync(HttpClient client)
    {
        var id = FileId();
        if (id == -1)
            return [];
        
        var msg = await client.GetAsync($"https://gamebanana.com/apiv11/File/{id}/RawFileList");
        var content = await msg.Content.ReadAsStringAsync();

        return content.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public string NameAsValidFilename()
    {
        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();

        return new string(Name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
    }

    public bool IsUrlValid()
    {
        return Url.StartsWith(@"https://gamebanana.com/", StringComparison.Ordinal);
    }

    private FileInfo? GetDownloadFileInfo(string folder)
    {
        FileInfo folderInfo = new(folder);
        FileInfo file = new($"{folder}/{NameAsValidFilename()}");
        if (!file.FullName.StartsWith(folderInfo.FullName))
            return null;

        return file;
    }

    public bool ExistsLocally(string folder)
    {
        var file = GetDownloadFileInfo(folder);
        if (file is null)
            return false;
        
        return file.Exists && file.Length == Size;
    }
    
    public async Task<FileInfo?> DownloadToAsync(HttpClient client, string folder)
    {
        if (!IsUrlValid()) return null;
        
        var file = GetDownloadFileInfo(folder);
        if (file is null)
            return null;

        if (file.Exists && file.Length == Size)
        {
            //Console.WriteLine($"Skipping {Name}, as its already downloaded.");
            return file;
        }

        file.Delete();

        Console.WriteLine($"Downloading mod zip: {Name} from {Url} to {file.FullName} [{(Size ?? 0) / 1024}kb]");

        var tries = 0;
        
        nextAttempt:
        try
        {
            await using var zipStream = await client.GetStreamAsync(Url);
            await using var fileStream = file.OpenWrite();
            await zipStream.CopyToAsync(fileStream);
            Console.WriteLine($"Downloaded {Name} successfully");
            return file;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download {Name}: {ex}");
            if (tries++ < 3)
            {
                await Task.Delay(1000 * tries);
                Console.WriteLine("Retrying...");
                goto nextAttempt;
            }
        }

        return null;
    }
}

public record GamebananaMod(
    [property: JsonPropertyName("CategoryId")] int? CategoryId,
    [property: JsonPropertyName("Screenshots")] IReadOnlyList<string> Screenshots,
    [property: JsonPropertyName("Description")] string Description,
    [property: JsonPropertyName("Views")] int? Views,
    [property: JsonPropertyName("GameBananaType")] string GameBananaType,
    [property: JsonPropertyName("TokenizedName")] IReadOnlyList<string> TokenizedName,
    [property: JsonPropertyName("UpdatedDate")] int? UpdatedDate,
    [property: JsonPropertyName("GameBananaId")] int? GameBananaId,
    [property: JsonPropertyName("Text")] string Text,
    [property: JsonPropertyName("ModifiedDate")] int? ModifiedDate,
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("PageURL")] string PageUrl,
    [property: JsonPropertyName("MirroredScreenshots")] IReadOnlyList<string> MirroredScreenshots,
    [property: JsonPropertyName("CreatedDate")] int? CreatedDate,
    [property: JsonPropertyName("Author")] string Author,
    [property: JsonPropertyName("CategoryName")] string CategoryName,
    [property: JsonPropertyName("Downloads")] int? Downloads,
    [property: JsonPropertyName("Likes")] int? Likes,
    [property: JsonPropertyName("Files")] IReadOnlyList<GamebananaFile> Files
)
{
    public GamebananaFile? GetLatestFile(IEnumerable<string> bannedNames)
        => (Files?.Where(f => f.IsUrlValid() && (f.HasEverestYaml ?? false) 
                                        && !bannedNames.Any(banned => f.Name.Contains(banned, StringComparison.OrdinalIgnoreCase))) ?? [])
            .MaxBy(f => f.CreatedDate ?? 0);

    public SimpleGamebananaMod ToSimpleMod() => new(GameBananaId, Name, CategoryName, Author, PageUrl);
}

public record SimpleGamebananaMod(
    [property: JsonPropertyName("GameBananaId")] int? GameBananaId,
    [property: JsonPropertyName("Name")] string Name,
    [property: JsonPropertyName("CategoryName")] string CategoryName,
    [property: JsonPropertyName("Author")] string Author,
    [property: JsonPropertyName("PageURL")] string PageUrl
);