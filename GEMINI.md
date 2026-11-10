# QuickMangaTool - Development Guidelines & Project Context

> **Note**: This file is automatically loaded by Antigravity at the start of every session to guide planning, architectural decisions, and code generation.

---

## 1. Project Context & Current Architecture

### 1.1 Overview & Tech Stack
- **Project**: `QuickMangaTool` - CLI & Interactive TUI utility for batch processing, converting, analyzing, and optimizing manga/comic archives (`.cbz`, `.cbr`, `.pdf`).
- **Runtime & Language**: .NET 10.0 (`net10.0`), C# 13, x64, Nullable enabled.
- **Key Dependencies**:
  - `NetVips`: High-performance image processing binding to system `libvips` (supporting WebP, AVIF, JXL).
  - `SharpCompress`: Archive reading and writing (ZIP, RAR, etc.).
  - `PdfPig`: Extracting images from PDF files.
  - `Spectre.Console`: Rich terminal UI, interactive menus, and progress bars.

### 1.2 Recent Major Architectural Changes
- **NetVips System Library Interop**: Removed `NetVips.Native` bundle in favor of system `libvips` (enabling full `libjxl` encoder/decoder support).
- **Interactive TUI & CLI Subcommands**: Added Spectre.Console interactive menu when invoked without arguments; organized subcommands (`convert`, `compress`, `pdf-to-cbz`, `cbr-to-cbz`, `upscale`, `remove-old`).
- **Pipeline Optimization**: Eliminated 2x redundant image encoding in `ArchiveProcessor` and isolated PDF image extraction.
- **Feature Cleanup**: Removed deprecated `genre-to-tags` and duplicate detection (`duplicates.db`/pHash).

### 1.3 Key Project Structure
```
QuickMangaTool/
├── Cli/             # AppOptions record, OptionsParser (command-line arguments & subcommands)
├── Commands/        # IAppCommand implementations (Batch, Upscale, Compress, CBR/PDF, Diagnostics)
├── Logging/         # ColoredFileLogger, IBatchLogger, IRunLogger
├── Processing/      # BatchProcessor, ArchiveProcessor, SkipCache
│   ├── Images/      # IArchiveImageConverter, Webp/Avif/Jxl converters, PdfImageExtractor
│   └── Upscale/     # NetVipsQualityAnalyzer, UpscaleDetector
├── Ui/              # InteractiveMenu, ProgressFactory, ProgressDescriptionFormatter
├── Utilities/       # ImageExtensions, ImageColorClassifier, SizeFormatter, StreamByteReader
└── Program.cs       # CLI & TUI Entrypoint & command dispatching
QuickMangaTool.Tests/ # Comprehensive unit & integration tests (xUnit)
```

### 1.4 Build & Test Quick Reference
```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~OptionsParserTests"
```

---

## 2. Core Engineering Rules & Principles

Every planning step and code change **MUST** adhere to the following 5 pillars:

### 2.1 Refactor & Clean Code
- **Single Responsibility (SRP)**: Keep command classes, processors, and utilities focused on a single concern.
- **Separation of Concerns**: Keep CLI parsing (`Cli/`), command orchestration (`Commands/`), and core logic (`Processing/`, `Utilities/`) decoupled.
- **Preserve Conventions**: Maintain existing file structures, naming conventions, and logging patterns (`Spectre.Console` + `IRunLogger`).

### 2.2 Optimization & Performance
- **Streaming over Buffering**: Stream archive entries and images wherever possible. Avoid reading entire large archives into intermediate byte arrays unless strictly required.
- **Parallelism Tuning**: Respect worker counts (`WorkerCount`, `--parallel=N`) tuned for disk I/O and CPU cores.
- **NetVips Pipeline Efficiency**: Chain NetVips operations lazily; avoid premature image evaluations or unnecessary format round-trips.

### 2.3 Duplicate Logic Elimination (DRY)
- **Centralize Common Operations**: Consolidate stream reading, temp file cleanup, archive entry filtering, and MIME/format detection into shared utilities or base services.
- **Re-use Options & Parser Logic**: Avoid duplicating flag extraction or validation across commands.

### 2.4 Edge Cases & Error Handling
- **Corrupt / Empty Inputs**: Safely handle 0-byte files, corrupted archives (`SharpCompress` exceptions), missing image headers, and unsupported formats without crashing the entire batch.
- **Path Traversal Protection**: Guard against "Zip Slip" vulnerabilities when extracting or writing archive entries.
- **Non-Destructive Defaults**: Maintain dry-run behavior (`--confirm` required for modifications) where destructive changes are possible.
- **Graceful Failure & Reporting**: Log individual file failures with informative messages via `IRunLogger` and continue processing remaining items when in batch mode.
- **Cancellation**: Honor `CancellationToken` in long-running batch and image processing loops.

### 2.5 Memory Leaks & Cleanup (Critical for NetVips & Native Interop)
- **Deterministic Disposal**: Any object implementing `IDisposable` or `IAsyncDisposable` **MUST** be disposed using `using` declarations, statements, or reliable `finally` blocks.
- **NetVips Image Lifetime**:
  - `NetVips.Image` wraps unmanaged C memory. **Every** intermediate image created in a pipeline must be disposed promptly to prevent native memory exhaustion.
  - Do not let `NetVips.Image` instances leak out of method scopes unless ownership transfer is clearly documented.
- **Streams & Temp Files**: Always close/dispose `FileStream`, `MemoryStream`, and temporary archive files, ensuring cleanup even when exceptions occur.
- **SQLite Connections**: Ensure connections and commands in `SQLiteDuplicateStore` are disposed properly.

---

## 3. Planning & Pre-Code Checklist
Before proposing or applying changes, verify:
1. [ ] Have duplicate implementations been avoided or consolidated?
2. [ ] Are all `IDisposable` resources (especially `NetVips.Image` and streams) properly wrapped in `using` blocks?
3. [ ] Are edge cases (empty files, corrupt archives, cancellation) handled gracefully?
4. [ ] Does this maintain backward compatibility with CLI options and tests?
5. [ ] Do existing unit tests pass, and are new tests added for new logic?
