# ArchiveIndexer

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

## Known gaps

See section 8 ("Known Technical Debt") of the developer guide for the full list —
notably, the `EnvironmentType` field is always empty today (nothing in the
filename convention supplies it), and the UI's "Prime Match" button currently
runs the same query as "Exact Match" by design.

## License

See [`LICENSE.md`](LICENSE.md).
