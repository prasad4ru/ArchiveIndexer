# ArchiveIndexer

## Search Flow

<img width="252" height="836" alt="image" src="https://github.com/user-attachments/assets/f043f397-1386-4e0c-9874-d63092b496b8" />


Indexes historical XML message files out of dated ZIP archives into a searchable
[Lucene.Net](https://lucenenet.apache.org/) index, and provides a desktop search
UI so a support engineer can find and extract a specific file in seconds instead
of opening ZIPs one at a time.

For the full architecture write-up, data-flow walkthrough, debugging playbook,
and configuration reference, see [`docs/ArchiveIndexer-Developer-Guide.docx`](docs/ArchiveIndexer-Developer-Guide.docx).
This README is the quick-start version.

## Solution layout

| Project | Purpose |
|---|---|
| `ArchiveIndexer.Core` | Models, interfaces, configuration POCOs. No logic. |
| `ArchiveIndexer.Infrastructure` | All real logic — parsing, scanning, Lucene indexing/searching, the file catalog, the live file watcher. |
| `ArchiveIndexer.Worker` | Windows background service. Scans the archive folder, watches for new/changed/deleted ZIPs, writes to the Lucene index. |
| `ArchiveIndexer.SearchUI` | WPF desktop app. Reads the same index the Worker writes to; search + one-click extract. |
| `ArchiveIndexer.Tests` | xUnit + Moq test suite. |
| `ArchiveIndexer.SearchTests` | Small standalone console harness for smoke-testing search modes against a throwaway index. |

## Requirements

- .NET 8 SDK
- Windows (the Worker targets Windows Service hosting; the UI is WPF, Windows-only)

## Getting started

1. Clone the repo and open `ArchiveIndexer.sln` (or open the folder in Visual Studio / Rider).
2. Edit `ArchiveIndexer.Worker/appsettings.json` and `ArchiveIndexer.SearchUI/appsettings.json` —
   set `ArchiveRoot` and `IndexPath` to real paths on your machine.
   **`IndexPath` must be identical in both files** — the Worker and the UI communicate
   purely by reading/writing the same Lucene index on disk, not through any API.
3. Set both `ArchiveIndexer.Worker` and `ArchiveIndexer.SearchUI` as startup projects
   (Solution Properties → Startup Project → Multiple startup projects) and run.
4. Drop a ZIP matching the naming convention below into `ArchiveRoot` and watch the
   Worker's console/log pick it up.

### ZIP naming convention

```
Mon_dd_yyyy_HH_mm_ss.zip     e.g.  Feb_16_2022_06_12_13.zip
```

### XML entry naming convention

```
SystemName_StoreCode_EnvironmentName_Sequence_MessageType_StartTicks_EndTicks.xml
```

## Running the tests

```
dotnet test ArchiveIndexer.Tests
```

## Publishing

Both `ArchiveIndexer.Worker` and `ArchiveIndexer.SearchUI` are pre-configured for
single-file, self-contained `win-x64` publish:

```
dotnet publish ArchiveIndexer.Worker\ArchiveIndexer.Worker.csproj -c Release -o publish\Worker
dotnet publish ArchiveIndexer.SearchUI\ArchiveIndexer.SearchUI.csproj -c Release -o publish\SearchUI
```

`appsettings.json` ships as a separate file next to each `.exe` on purpose, so
paths can be edited post-publish without a rebuild.

## NuGet packages

### ArchiveIndexer.Core
No packages — pure models/interfaces, .NET BCL only.

### ArchiveIndexer.Infrastructure
| Package | Version | Used for |
|---|---|---|
| Lucene.Net | 4.8.0-beta00018 | Core indexing/search engine |
| Lucene.Net.Analysis.Common | 4.8.0-beta00018 | Analyzers used by the index writer |
| Lucene.Net.Highlighter | 4.8.0-beta00018 | *(not currently used in code)* |
| Lucene.Net.Queries | 4.8.0-beta00018 | Query support (`BooleanQuery`, `NumericRangeQuery`) |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.10 | DI registration extension methods |
| Microsoft.Extensions.Hosting | 10.0.10 | `BackgroundService` base class |
| Microsoft.Extensions.Logging.Abstractions | 10.0.10 | `ILogger<T>` |
| Microsoft.Extensions.Options | 10.0.10 | `IOptions<ArchiveSettings>` |
| SharpCompress | 0.50.0 | *(not used in code)* |
| SharpZipLib | 1.4.2 | *(not used in code)* |
| System.Diagnostics.DiagnosticSource | 10.0.10 | Diagnostics/tracing infra |
| System.Threading.Channels | 10.0.10 | `DocumentQueue`'s underlying `Channel<T>` |

### ArchiveIndexer.Worker
| Package | Version | Used for |
|---|---|---|
| Microsoft.Extensions.Hosting | 10.0.10 | Generic Host |
| Microsoft.Extensions.Hosting.WindowsServices | 10.0.10 | `AddWindowsService()` |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.10 | Binding `appsettings.json` to `ArchiveSettings` |
| Serilog.AspNetCore | 10.0.0 | Logging pipeline |
| Serilog.Sinks.Console | 6.1.1 | Console log output |
| Serilog.Sinks.File | 7.0.0 | Rolling file log output |

### ArchiveIndexer.SearchUI
| Package | Version | Used for |
|---|---|---|
| Microsoft.Extensions.Configuration | 10.0.10 | Reading `appsettings.json` |
| Microsoft.Extensions.Configuration.Json | 10.0.10 | JSON config provider |
| Microsoft.Extensions.Configuration.Binder | 10.0.10 | `GetSection(...).Bind(...)` |
| Microsoft.Extensions.Options | 10.0.10 | `IOptions<ArchiveSettings>` |

### ArchiveIndexer.Tests
| Package | Version | Used for |
|---|---|---|
| Microsoft.NET.Test.Sdk | 17.11.1 | Test host |
| xunit | 2.9.2 | Test framework |
| xunit.runner.visualstudio | 2.8.2 | Test discovery/runner |
| coverlet.collector | 6.0.2 | Code coverage collection |
| Moq | 4.20.72 | Mocking |

### ArchiveIndexer.SearchTests
| Package | Version | Used for |
|---|---|---|
| Microsoft.Extensions.Options | 10.0.10 | `IOptions<ArchiveSettings>` |

> **Note:** `SharpCompress`, `SharpZipLib`, and `Lucene.Net.Highlighter` are
> referenced but not used anywhere in the current code — everything ZIP-related
> uses the built-in `System.IO.Compression` instead. Pre-existing from before
> this codebase was picked up, not a recent addition. Safe cleanup candidates
> whenever someone wants to trim the published exe size.

## Known gaps

See section 8 ("Known Technical Debt") of the developer guide for the full list —
notably, the `EnvironmentType` field is always empty today (nothing in the
filename convention supplies it), and the UI's "Prime Match" button currently
runs the same query as "Exact Match" by design.

## License

See [`LICENSE.md`](LICENSE.md).
