---
description: 'Guidelines for building C# console applications'
applyTo: '**/*.cs'
---

# C# Development — QuickMangaTool Console App

## General Guidelines
- QuickMangaTool is a C# .NET 10 console app. 
- Always enforce SOLID principles in all aspects of development.
- Write clear and concise comments for each function.

## C# Instructions
- Always use the latest version of C#, currently C# 14 features.

## General Instructions
- Make only high confidence suggestions when reviewing code changes.
- Write code with good maintainability practices, including comments on why certain design decisions were made.
- Handle edge cases and write clear exception handling.
- For libraries or external dependencies, mention their usage and purpose in comments.

## SOLID Principles
- **Single Responsibility**: Each class has one reason to change. Keep commands, processors, transformations, and loggers in separate, focused classes.
- **Open/Closed**: Extend behavior by adding new implementations (e.g., new `IXmlTransformation` or `IAppCommand`), never by modifying existing ones.
- **Liskov Substitution**: Implementations must be fully substitutable for their interfaces without altering correctness (e.g., any `IArchiveProcessor` must behave correctly wherever the interface is used).
- **Interface Segregation**: Define narrow, focused interfaces (e.g., `IEntryLogger`, `IRunLogger`, `IBatchLogger`) rather than large general-purpose ones.
- **Dependency Inversion**: Depend on abstractions, not concrete types. Inject dependencies via constructors and rely on interfaces throughout the codebase.

## Naming Conventions
- Follow PascalCase for class names, method names, and public members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g., `IAppCommand`, `IArchiveProcessor`).

## Formatting
- Apply code-formatting style defined in `.editorconfig`.
- Prefer file-scoped namespace declarations and single-line using directives.
- Insert a newline before the opening curly brace of any code block (e.g., after `if`, `for`, `while`, `foreach`, `using`, `try`, etc.).
- Ensure that the final return statement of a method is on its own line.
- Use pattern matching and switch expressions wherever possible.
- Use `nameof` instead of string literals when referring to member names.
- Ensure that XML doc comments are created for any public APIs. When applicable, include `<example>` and `<code>` documentation in the comments.

## Project Structure
- Organize code by feature folders: `Cli/`, `Commands/`, `Processing/`, `Logging/`, `Ui/`.
- New commands implement `IAppCommand` and are self-contained in the `Commands/` folder.
- New processing steps implement the appropriate interface (e.g., `IArchiveProcessor`, `IXmlTransformation`) and live in `Processing/`.
- `Program.cs` wires up dependencies and delegates execution to commands; keep it thin.

## Nullable Reference Types
- Declare variables non-nullable, and check for `null` at entry points.
- Always use `is null` or `is not null` instead of `== null` or `!= null`.
- Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

## Validation and Error Handling
- Validate CLI arguments and inputs at the entry point (`OptionsParser`, `Program.cs`) and fail fast with a clear message.
- Use specific exception types where meaningful; avoid swallowing exceptions silently.
- Surface errors to the user via the logging layer (`IEntryLogger`, `IRunLogger`) rather than writing directly to `Console`.

## Logging
- Always log through the `IBatchLogger`, `IRunLogger`, or `IEntryLogger` interfaces — never write directly to `Console` inside business logic.
- Log at the appropriate level: informational progress via the UI layer (`ProgressFactory`, `ProgressDescriptionFormatter`), errors and warnings through loggers.
- Keep logging concerns out of processing and transformation classes.

## Testing
- Always include test cases for critical paths of the application.
- Guide users through creating unit tests.
- Do not emit "Act", "Arrange" or "Assert" comments.
- Copy existing style in nearby files for test method names and capitalization.
- Demonstrate how to mock dependencies (e.g., `IArchiveProcessor`, `IBatchLogger`) for effective unit testing.
- Prefer testing against interfaces so implementations remain independently replaceable.

## Performance Optimization
- Use `async`/`await` for I/O-bound work (file reads, archive extraction) to keep the console app responsive.
- Leverage `WorkerCount` to control parallelism in batch operations; avoid unbounded parallelism.
- Prefer streaming or chunked processing over loading entire archives into memory.