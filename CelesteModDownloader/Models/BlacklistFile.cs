namespace CelesteModDownloader.Models;

public record BlacklistFile(
    List<int> BannedMods,
    List<string> BannedFilenames
);
