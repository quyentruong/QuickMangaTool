# QuickMangaTool

A C# .NET 10 command-line utility for bulk processing, optimizing, and analyzing manga/comic archives (`.cbz`, `.cbr`, `.pdf`).

## Features

- **Interactive TUI Menu**: Run `QuickMangaTool` without arguments to launch an interactive terminal menu powered by Spectre.Console.
- **Image Conversion (WebP, AVIF, or JPEG XL)**: Converts images inside `.cbz` archives to a chosen format when it results in a smaller file, preserving original images if they are smaller. Default is WebP; WebP, AVIF, and JPEG XL all support a configurable `--effort` setting.
- **Archive Creation**:
  - `compress`: Compresses subfolders containing loose images into `.cbz` files.
  - `pdf-to-cbz`: Extracts embedded images from PDF files directly into `.cbz` files.
  - `cbr-to-cbz`: Converts `.cbr` archives recursively to `.cbz`.
- **Quality & Upscale Detection**: Samples images from each archive and flags pages that appear low-resolution, blurry, or noisy using Laplacian variance and Immerkaer noise analysis.
- **Optional Source Deletion**: `--delete-source` flag allows removing the original `.cbr`, `.pdf`, or subfolder once `.cbz` creation succeeds.
- **Opt-in Dry-run**: Real modification by default; `--dry-run` (`-n`) previews image conversion size savings without modifying archives.
- **Skip Cache**: Automatically caches clean archives (`~/.quickmangatool/skipcache.dat`) so re-runs skip unchanged files.
- **Parallel Processing**: Configurable worker count tuned for CPU and disk I/O with rich Spectre.Console progress bars.
- **Timestamped Logs**: Detailed colored logs generated per batch run.

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
QuickMangaTool/bin/Release/net10.0/linux-x64/publish/QuickMangaTool
```

## Usage

```bash
# Launch interactive menu
QuickMangaTool

# Or run specific commands directly
QuickMangaTool [path] [command|options]
```

If `path` is omitted, the current working directory is used.

### Commands

| Command | Short / Alias | Description |
|---|---|---|
| `convert` | `-w`, `--convert-to-webp` | Convert images in `.cbz` archives to WebP/AVIF/JXL when smaller |
| `compress` | `--compress-folders` | Compress each subfolder into an individual `.cbz` |
| `pdf-to-cbz` | `--pdf-to-cbz` | Extract images from PDFs into `.cbz` archives |
| `cbr-to-cbz` | `--cbr-to-cbz` | Recursively convert `.cbr` archives to `.cbz` |
| `upscale` | `-u`, `--detect-upscale` | Scan archive images for resolution, blur, or noise issues |
| `remove-old` | `--remove-old-cbz` | Delete leftover `.old.cbz` backup files |
| `read-logs` | `--read-logs` | Interactively view run log files |
| `delete-logs` | `--delete-logs` | Delete all `.log` files in the target directory |
| `diag` | `--vips-diagnostics` | Print NetVips & system libvips codec configuration |
| `clear-cache` | `-c`, `--clear-cache` | Clear the persistent skip cache |
| `update` | `--update` | Check and update QuickMangaTool from GitHub Releases |

### Options

| Flag | Short | Description |
|---|---|---|
| `--interactive` | `-i` | Launch interactive menu |
| `--dry-run` | `-n` | Preview changes without modifying `.cbz` files (used with `convert`) |
| `--confirm` | `-y` | Confirm deletions or prompts automatically |
| `--delete-source` | | Delete original file/folder after successful `.cbz` creation (`compress`, `pdf-to-cbz`, `cbr-to-cbz`) |
| `--image-format=webp\|avif\|jxl` | | Target output image format (default: `webp`) |
| `--effort=N` | | Encoder effort level (WebP: `0-6`, JXL: `1-9`, AVIF: `0-9`) |
| `--parallel=N` | | Worker thread count (default: tuned for CPU/disk) |
| `--help` | `-h` | Show help and exit |

### Examples

```bash
# Launch interactive menu
QuickMangaTool

# Convert images in C:\manga to WebP (modifies archives, keeps .old.cbz backup)
QuickMangaTool C:\manga convert

# Dry-run: preview size savings without writing changes
QuickMangaTool C:\manga convert -n

# Convert to AVIF with effort 6 and delete .old.cbz backups after conversion
QuickMangaTool C:\manga convert --image-format=avif --effort=6 --remove-old-cbz

# Convert to JPEG XL with effort 7
QuickMangaTool C:\manga convert --image-format=jxl --effort=7

# Compress subfolders into .cbz and delete source folders on success
QuickMangaTool /path/to/manga compress --delete-source

# Extract all PDFs in a folder to CBZ and remove source PDFs
QuickMangaTool /path/to/pdfs pdf-to-cbz --delete-source

# Convert CBR archives to CBZ and remove source CBRs
QuickMangaTool /path/to/cbrs cbr-to-cbz --delete-source

# Scan for archives that may need upscaling (read-only)
QuickMangaTool /path/to/manga upscale

# Delete all leftover .old.cbz backups without asking for confirmation
QuickMangaTool /path/to/manga remove-old -y

# Verify libvips system installation and codec support (WebP, HEIF/AVIF, JXL)
QuickMangaTool --vips-diagnostics

# Check for and apply updates from GitHub Releases
QuickMangaTool update
```

## Requirements

- .NET 10 runtime
- Linux (x64) or Windows (x64)
- `libvips` installed on system (with WebP, AVIF/HEIF, JXL, JPEG, PNG, GIF, and TIFF support):
  - **Windows (WinGet)**:
    ```powershell
    winget install -e --id libvips.libvips
    ```
    *(Restart your terminal after installation so `PATH` is refreshed.)*
  - **Linux**:
    - **Ubuntu / Debian**:
      ```bash
      sudo apt update && sudo apt install -y libvips libheif-plugin-aomenc libheif-plugin-dav1d
      ```
    - **Arch / Manjaro**:
      ```bash
      sudo pacman -S --needed libvips libwebp libheif libjxl aom dav1d libjpeg-turbo libpng libtiff cgif
      ```
    - **Fedora / RHEL**:
      ```bash
      sudo dnf install -y vips libheif-freeworld
      ```
    - **openSUSE**:
      ```bash
      sudo zypper install -y libvips42 libheif-aom libheif-dav1d
      ```
    - **Alpine Linux**:
      ```bash
      sudo apk add vips vips-heif vips-jxl
      ```
