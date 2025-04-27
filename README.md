# CelesteModDownloader

A CLI tool for bulk-downloading and updating Celeste mods.

## Example Usage

Download/update all mods in the GameBanana category 575 (tools:other/misc) and 5081 (helpers) into the directory `./test2`
```bash
.\CelesteModDownloader.exe update -d ./test2 -c 575,5081
```

Download/update all helper-like mods (GameBanana categories 575,5081,4632), used by the IL Hook Viewer:
```bash
.\CelesteModDownloader.exe update-helpers -d ./test2
```

## Blacklist

Place a `modDownloaderBlacklist.json` in the download directory to blacklist downloading certain mods/files. Example file:
```json
{
    "BannedMods": [ // List of gamebanana id's to ban:
        412041,
        53631,
        368222,
    ],
    "BannedFilenames": [ // List of filenames not to download, even though they are the most recent file:
        "clutterhelperexamplemap"
    ]
}
```