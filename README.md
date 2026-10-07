# QuickMangaTool

A C# .NET 10 command-line utility for bulk processing manga archives in `.cbz` format. It recursively scans a directory for `.cbz` files and can transform embedded XML metadata or analyze image quality to identify archives that need upscaling or restoration.

## Features

- **Genre → Tags migration**: Renames `<Genre>` elements to `<Tags>` in every `ComicInfo.xml` inside each archive, migrating from the old ComicInfo field to the newer standard.
- **Image conversion (WebP or AVIF)**: Converts images in `.cbz` archives to a chosen output format when it results in a smaller file, preserving the original if it is smaller. The default output format is WebP; AVIF is supported via Magick.NET (see Requirements / notes). Supported input formats: JPG, PNG, BMP, GIF, TIFF, AVIF.
- **Upscale detection**: Samples images from each archive and flags pages that appear low-resolution, blurry, or noisy using Laplacian variance and Immerkaer noise analysis.
- **Dry-run by default**: No files are modified unless `--confirm` is explicitly passed.
- **Skip cache**: Previously processed archives are cached so re-runs skip unchanged files automatically.
- **Parallel processing**: Configurable worker count (default 2, capped at `min(CPU×2, 8)`) with a live Spectre.Console progress bar.
- **Timestamped log file**: A `.log` file is written to the target directory on every run.

## Build on Linux

From the repository root:

```bash
# Restore dependencies
dotnet restore QuickMangaTool.slnx

# Build in Release mode
dotnet build QuickMangaTool.slnx -c Release

# Publish a self-contained Linux x64 binary
dotnet publish QuickMangaTool/QuickMangaTool.csproj -c Release -r linux-x64 --self-contained true
```

Published output will be under:

```text
QuickMangaTool/bin/Release/net10.0/linux-x64/publish/
```

Run it on Linux:

```bash
./QuickMangaTool/bin/Release/net10.0/linux-x64/publish/QuickMangaTool

```

## Test coverage

You can collect test coverage with Coverlet and render an HTML report using ReportGenerator.

1. Add Coverlet to the test project (one-time):

```bash
dotnet add QuickMangaTool.Tests/QuickMangaTool.Tests.csproj package coverlet.msbuild
```

2. Run tests and produce an OpenCover-format coverage file:

```bash
dotnet test QuickMangaTool.slnx --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./coverage/
```

3. Install ReportGenerator (global tool) and create an HTML report:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.opencover.xml" -targetdir:"coverage-report" -reporttypes:Html
```

4. Open the report by opening `coverage-report/index.htm` in your browser.

This writes coverage files under `TestResults/`; you can point ReportGenerator at the produced coverage XML files to generate the same HTML output.

## Log commands

Two standalone commands help manage run log files written to the target directory:

- `--read-logs` — interactively read one or more `.log` files found under the given path.
	- If a single `.log` file is found it is printed immediately.
	- If multiple are found you are shown a numbered list and prompted to choose by number, a comma-separated list (e.g. `1,3`), or `all` to print every log.

- `--delete-logs` — deletes all `.log` files found under the given path. Returns non-zero if any deletions fail.

Both commands accept an optional path as the first argument. Examples:

```bash
# Read logs under the current directory (interactive selection)
QuickMangaTool --read-logs

# Read logs under a specific directory
QuickMangaTool /path/to/manga --read-logs

# Delete all logs under a directory
QuickMangaTool /path/to/manga --delete-logs
```
```

## Usage

```
QuickMangaTool [path] [options]
```

If `path` is omitted, the current directory is used.

### Options

| Flag | Short | Description |
|---|---|---|
| `--help` | `-h` | Show help and exit |
| `--dry-run` | `-n` | Preview changes without modifying any files (default) |
| `--confirm` | `-y` | Apply changes to `.cbz` files |
| `--genre-to-tags` | `-g` | Apply the Genre → Tags XML metadata transformation |
| `--convert-to-webp` | `-w` | Convert images in archives to WebP (or another chosen format) when smaller than the original |
| `--image-format=webp|avif` | | Choose output image format when converting or compressing (default: `webp`) |
| `--compress-folders`, `--compress` | | Compress every subfolder under the given path into an individual `.cbz` |
| `--pdf-to-cbz` | | Extract images from PDFs in a folder and produce `.cbz` files next to each PDF (non-recursive) |
| `--cbr-to-cbz` | | Recursively convert `.cbr` archives to `.cbz` |
| `--detect-upscale` | `-u` | Scan archive images for quality issues |
| `--remove-old-cbz` | | Remove `.old.cbz` backup files after conversion (or run standalone to delete leftover backups) |
| `--parallel=N` | | Set parallel worker count (default: tuned for system) |
| `--phash-threshold=N` | | Maximum Hamming distance for pHash duplicate matching (default: 8) |
| `--read-logs` | | Interactively read `.log` files found under the target path |
| `--delete-logs` | | Delete `.log` files found under the target path |
| `--clear-cache` | `-c` | Delete the persistent skip cache and exit |

### Examples

```bash
# Dry-run (preview) in the current directory
QuickMangaTool.exe

# Dry-run in a specific directory
QuickMangaTool.exe C:\manga -n

# Apply Genre→Tags transformation
QuickMangaTool.exe C:\manga -g -y

# Scan for archives that may need upscaling (read-only)
QuickMangaTool.exe C:\manga -u

# Apply multiple operations at once
QuickMangaTool.exe C:\manga -g -u -y

# Clear the skip cache
QuickMangaTool.exe --clear-cache

# Convert all PDFs in a folder to CBZ (non-recursive). For each `sample.pdf` the tool
# will create `sample.cbz` next to the PDF, skipping any PDFs that already have a
# corresponding `.cbz` file.
QuickMangaTool.exe /path/to/folder --pdf-to-cbz
```

## Commands

### WebP Image Conversion (`--convert-webp`)

Finds all supported images (JPG, PNG, BMP, GIF, TIFF, AVIF) in each `.cbz` archive and converts them to WebP format if the resulting file is smaller. If the original image is smaller, it is kept instead. This process helps reduce archive size without sacrificing image quality. The final archive may contain a mix of original and WebP images, depending on which format is smaller for each entry.

In dry-run mode, reports how many images *would* be converted without writing to disk.

### Genre → Tags (`--genre-to-tags`)

Finds every `ComicInfo.xml` inside each `.cbz` and renames all `<Genre>` elements to `<Tags>`, preserving child nodes and attributes. Archives that require no change are recorded in the skip cache and skipped on future runs.

In dry-run mode, reports how many archives *would* be updated without writing to disk.

### Upscale Detection (`--detect-upscale`)

Always read-only. Samples up to 10 images per archive (the 5 smallest and 5 largest by uncompressed size) and runs four quality checks:

| Check | Threshold | Notes |
|---|---|---|
| Minimum width | 800 px | Images narrower than 300 px (ads/banners) are excluded |
| DPI | ≥ 72 DPI | Only checked when DPI metadata is present |
| Blur (Laplacian σ²) | ≥ 50.0 | Title cards and chapter dividers (low edge density) are excluded |
| Noise (Immerkaer σ) | ≤ 12.5 | High-contrast pages are excluded to avoid false positives |

Archives with at least one flagged image are reported as needing upscaling. Clean archives are added to the skip cache.

## Skip Cache

Stored at `~/.quickmangatool/skipcache.dat`. Each entry records the archive path and its last-modified timestamp — if the file changes, it is automatically re-processed. Use `--clear-cache` to remove the cache entirely.

## Requirements

- .NET 10 runtime
- Windows or Linux (x64)