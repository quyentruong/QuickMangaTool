using System.Runtime.CompilerServices;

// Expose internal types (those in the global namespace with no explicit access modifier)
// to the unit test project so they can be tested without making them public.
[assembly: InternalsVisibleTo("QuickMangaTool.Tests")]

// Required by Moq / Castle.Core so it can generate runtime proxy types for internal interfaces
// (e.g. IImageQualityAnalyzer) when mocking inside the test project.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
