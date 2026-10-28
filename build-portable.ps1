$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$root = $PSScriptRoot
$project = Join-Path $root 'source\VideoDownloader.csproj'
$tools = Join-Path $root 'tools'
$exe = Join-Path $root 'EasyVideoDownloader.exe'
$archive = Join-Path $root 'EasyVideoDownloader-portable.zip'

New-Item -ItemType Directory -Force -Path $tools | Out-Null
$ytDlp = Join-Path $tools 'yt-dlp.exe'
if (!(Test-Path $ytDlp) -or (Get-Item $ytDlp).Length -lt 20000000) {
    Write-Host 'Baixando yt-dlp oficial…'
    $partial = Join-Path $tools 'yt-dlp.download'
    Invoke-WebRequest 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile $partial
    if ((Get-Item $partial).Length -lt 20000000) { throw 'O download do yt-dlp parece incompleto.' }
    Move-Item $partial $ytDlp -Force
}

$ffmpeg = Join-Path $tools 'ffmpeg.exe'
if (!(Test-Path $ffmpeg) -or (Get-Item $ffmpeg).Length -lt 10000000) {
    Write-Host 'Baixando FFmpeg para combinar vídeo e áudio…'
    $ffmpegArchive = Join-Path $tools 'ffmpeg-portable.zip'
    $ffmpegExtract = Join-Path $tools '.ffmpeg-extract'
    Invoke-WebRequest 'https://github.com/BtbN/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip' -OutFile $ffmpegArchive
    Expand-Archive -LiteralPath $ffmpegArchive -DestinationPath $ffmpegExtract -Force
    $ffmpegSource = Get-ChildItem $ffmpegExtract -Filter 'ffmpeg.exe' -Recurse -File | Select-Object -First 1
    if ($null -eq $ffmpegSource -or $ffmpegSource.Length -lt 10000000) { throw 'O pacote do FFmpeg parece incompleto.' }
    Copy-Item -LiteralPath $ffmpegSource.FullName -Destination $ffmpeg -Force
    Remove-Item -LiteralPath $ffmpegArchive -Force
    Remove-Item -LiteralPath $ffmpegExtract -Recurse -Force
}

Write-Host 'Publicando aplicativo Windows x64 autocontido…'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $root
Remove-Item (Join-Path $root 'EasyVideoDownloader.pdb') -ErrorAction SilentlyContinue
Get-ChildItem $root -Filter 'Microsoft.Web.WebView2.*.xml' -File | Remove-Item -Force

if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -LiteralPath @($exe, $tools, (Join-Path $root 'README.md')) -DestinationPath $archive -CompressionLevel Optimal
Write-Host "Pronto: $exe"
Write-Host "Pacote portátil: $archive"
