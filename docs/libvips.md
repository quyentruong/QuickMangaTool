libvips (native) — installation and CI notes

NetVips is a .NET binding for the native `libvips` image-processing library. The `NetVips` NuGet package requires a compatible `libvips` native library to be present on the host system (developer machines, CI, and production runners).

This page contains recommended install commands for common platforms and an example GitHub Actions workflow that installs `libvips` and runs a `dotnet` build/test.

## Ubuntu / Debian

Install the native package via `apt`:

```bash
sudo apt-get update
sudo apt-get install -y libvips libvips-dev libvips-tools
```

On older distributions you may need to enable a newer package source (or build libvips from source) if the distro-provided version is too old. NetVips v3.2.0 works best with libvips >= 8.12 but often works with slightly older releases; test in CI.

## Fedora / RHEL

On Fedora or RHEL derivatives use `dnf` or `yum`:

```bash
sudo dnf install -y vips vips-devel vips-tools
```

On RHEL you may need to enable EPEL or use a third-party repository.

## macOS (Homebrew)

Install with Homebrew:

```bash
brew update
brew install vips
```

## Windows

Two practical approaches on Windows CI or developer machines:

- **MSYS2 (recommended for GitHub Actions):** use `msys2`/`pacman` to install the `mingw`-packaged `vips` runtime.
- **Prebuilt binaries:** download prebuilt libvips artifacts from the libvips releases page (https://github.com/libvips/libvips/releases) and add the `bin` directory to `PATH`.

MSYS2 example (developer or CI):

1. Install MSYS2 (https://www.msys2.org/) and open the MSYS2 MinGW 64-bit shell.
2. Install the vips package:

```bash
pacman -Syu --noconfirm
pacman -S --noconfirm mingw-w64-x86_64-vips
```

3. Ensure the `mingw64` bin directory is on `PATH` (e.g. `C:\msys64\mingw64\bin`).

## GitHub Actions example

Below is a minimal workflow that installs `libvips` on Ubuntu/macOS/Windows runners and runs `dotnet build` and `dotnet test`.

Place this workflow under `.github/workflows/libvips.yml`.

Key points:
- Ubuntu: installs `libvips` via `apt`.
- macOS: installs via `brew`.
- Windows: installs via MSYS2 and adds the MinGW64 `bin` to `PATH` so `NetVips` can find the native library.

```yaml
name: CI - Install libvips and run tests

on:
  push:
  pull_request:

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, macos-latest, windows-latest]

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install libvips (Ubuntu)
        if: matrix.os == 'ubuntu-latest'
        run: |
          sudo apt-get update
          sudo apt-get install -y libvips libvips-dev libvips-tools

      - name: Install libvips (macOS)
        if: matrix.os == 'macos-latest'
        run: |
          brew update
          brew install vips

      - name: Install libvips (Windows via MSYS2)
        if: matrix.os == 'windows-latest'
        uses: msys2/setup-msys2@v2
        with:
          msystem: MINGW64
          update: true

      - name: Install vips on Windows (pacman)
        if: matrix.os == 'windows-latest'
        run: |
          pacman -S --noconfirm mingw-w64-x86_64-vips
          echo "C:\\msys64\\mingw64\\bin" >> $GITHUB_PATH

      - name: Restore
        run: dotnet restore QuickMangaTool.slnx

      - name: Build
        run: dotnet build QuickMangaTool.slnx -c Release --no-restore

      - name: Test
        run: dotnet test QuickMangaTool.Tests/QuickMangaTool.Tests.csproj --no-build --verbosity normal

```

## Notes and troubleshooting

- If `NetVips` throws errors about not finding the native library, ensure the `libvips` runtime is available on `PATH` (Windows) or in the system library search path (`LD_LIBRARY_PATH`/`DYLD_LIBRARY_PATH`) on Linux/macOS. The workflow above ensures the runtime is installed and available.
- If CI runners provide an older `libvips` version that lacks required codecs, consider installing a newer release from the libvips project or building from source.
- When testing locally, ensure you restart your terminal or add the `libvips` `bin` path to your session's `PATH`.

## Further reading

- libvips project: https://libvips.github.io/libvips/
- NetVips (NuGet): https://www.nuget.org/packages/NetVips/
