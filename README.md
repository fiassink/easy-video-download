# Easy Video Downloader

A portable Windows desktop app for checking video formats and downloading selected qualities with [yt-dlp](https://github.com/yt-dlp/yt-dlp). The interface follows the Windows display language when Portuguese is selected; English is used for other system languages. The product name remains **Easy Video Downloader**.

## Features

- Lists formats reported for the supplied URL, including resolution and orientation.
- Downloads the selected format without silently changing quality.
- Supports album pages and filters repeated media URLs before downloading.
- Shows video title, duration, and thumbnail when the site provides accessible metadata.
- Uses an embedded WebView fallback when yt-dlp cannot extract a supported page.
- Allows an in-progress check or download to be canceled.

## Build a portable release

Requirements: Windows 10/11 x64, PowerShell, and the .NET 8 SDK. The build script downloads yt-dlp and an FFmpeg build into `tools/`, publishes a self-contained executable, and creates a portable ZIP.

```powershell
./build-portable.ps1
```

The output files are `EasyVideoDownloader.exe` and `EasyVideoDownloader-portable.zip`.

## Notes

Support depends on yt-dlp extractors and each site's access rules. The app does not bypass DRM, paywalls, or access controls. Download only content you are authorized to save and follow the source site's terms.

## Third-party tools

- [yt-dlp](https://github.com/yt-dlp/yt-dlp), licensed under The Unlicense.
- [FFmpeg](https://ffmpeg.org/), included in portable builds; the build script uses a GPL build.
- Microsoft WebView2 SDK, used by the browser fallback.
