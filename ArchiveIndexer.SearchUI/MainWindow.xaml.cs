using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Parsing;
using ArchiveIndexer.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace ArchiveIndexer.SearchUI;

public partial class MainWindow : Window
{
    private const string Placeholder = "Enter exact XML filename (e.g. ATD_MARS_Stage_20231027_034000_Event.xml)";

    private static readonly Brush InfoColor = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
    private static readonly Brush SuccessColor = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly Brush WarningColor = new SolidColorBrush(Color.FromRgb(0xFA, 0xCC, 0x15));
    private static readonly Brush ErrorColor = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    private readonly ObservableCollection<LogEntry> _log = new();
    private readonly ObservableCollection<SearchResultRow> _searchResults = new();
    private readonly LuceneSearcher _searcher;
    private readonly string _extractOutputPath;

    public MainWindow()
    {
        InitializeComponent();

        LogItems.ItemsSource = _log;
        ResultsItems.ItemsSource = _searchResults;

        FileNameTextBox.Text = Placeholder;
        FileNameTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var archiveSettings = new ArchiveSettings();
        configuration.GetSection(nameof(ArchiveSettings)).Bind(archiveSettings);

        var uiSettings = new SearchUISettings();
        configuration.GetSection(nameof(SearchUISettings)).Bind(uiSettings);
        _extractOutputPath = string.IsNullOrWhiteSpace(uiSettings.ExtractOutputPath)
            ? Path.Combine(AppContext.BaseDirectory, "ExtractedFiles")
            : uiSettings.ExtractOutputPath;

        var options = Options.Create(archiveSettings);
        var queryBuilder = new SearchQueryBuilder(new XmlFileNameParser());
        _searcher = new LuceneSearcher(queryBuilder, options);

        UpdateDaysPanelVisibility();

        AppendLog(LogLevel.Info, "System initialized. Ready for retrieval operations.");

        Closed += (_, _) => _searcher.Dispose();
    }

    private void FileNameTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (FileNameTextBox.Text == Placeholder)
        {
            FileNameTextBox.Text = string.Empty;
            FileNameTextBox.Foreground = Brushes.White;
        }
    }

    private void FileNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FileNameTextBox.Text))
        {
            FileNameTextBox.Text = Placeholder;
            FileNameTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
        }
    }

    private async void FileNameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await RunSearchAsync();
        }
    }

    private async void LocateButton_Click(object sender, RoutedEventArgs e)
    {
        await RunSearchAsync();
    }

    private void SearchMode_Checked(object sender, RoutedEventArgs e)
    {
        // Fires once during InitializeComponent before DaysPanel is wired up yet - guard it.
        if (DaysPanel == null)
            return;

        UpdateDaysPanelVisibility();
    }

    private void UpdateDaysPanelVisibility()
    {
        // Only Set Match uses a date window - Exact Match is a single filename lookup.
        DaysPanel.Visibility = SetMatchModeRadio.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private ArchiveIndexer.Core.Models.SearchMode GetSelectedMode()
    {
        if (SetMatchModeRadio.IsChecked == true) return ArchiveIndexer.Core.Models.SearchMode.SetMatch;

        // The UI only exposes Exact Match and Set Match now. The richer
        // SystemName/StoreCode/Environment/MessageType + date-window query
        // (SearchQueryBuilder.BuildPrimeMatch / SearchMode.PrimeMatch) still exists
        // for other callers (e.g. ArchiveIndexer.SearchTests) - it's just not
        // reachable from this window anymore.
        return ArchiveIndexer.Core.Models.SearchMode.Exact;
    }

    private int GetSelectedDays()
    {
        if (int.TryParse(DaysTextBox.Text, out var days) && days > 0)
            return days;

        return 2; // fallback if the box is empty/invalid
    }

    private async Task RunSearchAsync()
    {
        var fileName = SanitizeFileName(FileNameTextBox.Text);

        if (string.IsNullOrWhiteSpace(fileName) || fileName == Placeholder)
        {
            AppendLog(LogLevel.Warning, "Enter an exact XML filename before searching.");
            return;
        }

        if (fileName != FileNameTextBox.Text.Trim())
        {
            // Reflect the cleaned-up value back so the user can see what was actually searched.
            FileNameTextBox.Text = fileName;
        }

        var mode = GetSelectedMode();
        var days = mode == ArchiveIndexer.Core.Models.SearchMode.Exact ? 0 : GetSelectedDays();

        LocateButton.IsEnabled = false;
        _searchResults.Clear();
        ResultsPanel.Visibility = Visibility.Collapsed;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (mode == ArchiveIndexer.Core.Models.SearchMode.Exact)
            {
                AppendLog(LogLevel.Info, $"Initializing extraction workflow for: {fileName}");
            }
            else
            {
                AppendLog(LogLevel.Info, $"Initializing Set Match search (\u00b1{days} days) seeded from: {fileName}");
            }

            AppendLog(LogLevel.Info, "Querying search index...");

            var results = await _searcher.SearchAsync(new SearchRequest
            {
                Mode = mode,
                FileName = fileName,
                Days = days,
                PageSize = 100
            }, CancellationToken.None);

            if (results.Count == 0)
            {
                AppendLog(LogLevel.Error, "No match found in index for that filename.");
                return;
            }

            if (mode == ArchiveIndexer.Core.Models.SearchMode.Exact)
            {
                await ExtractExactMatchAsync(fileName, results);
            }
            else
            {
                AppendLog(LogLevel.Success, $"{results.Count} matching document(s) found (Set Match).");

                // Same filename can legitimately exist in more than one ZIP (we've seen
                // this happen). Both stay clickable/downloadable - the user decides - but
                // since extraction always writes to the same "{ExtractOutputPath}\{FileName}"
                // path, clicking more than one duplicate will silently overwrite the file
                // from the previous click. Flag it up front instead of surprising them.
                var duplicateFileNames = results
                    .GroupBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var match in results)
                {
                    _searchResults.Add(new SearchResultRow(match, duplicateFileNames.Contains(match.FileName)));
                }

                if (duplicateFileNames.Count > 0)
                {
                    AppendLog(LogLevel.Warning,
                        $"{duplicateFileNames.Count} filename(s) appear in more than one archive - extracting more than one will overwrite the same output file. Marked below.");
                }

                ResultsPanel.Visibility = Visibility.Visible;

                AppendLog(LogLevel.Info, $"Click any filename above to extract it to: {_extractOutputPath}");
            }
        }
        catch (Exception ex)
        {
            AppendLog(LogLevel.Error, $"Error: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            AppendLog(LogLevel.Info, $"Audit tracking complete. Process finished in {stopwatch.Elapsed.TotalSeconds:0.0} seconds.");
            LocateButton.IsEnabled = true;
        }
    }

    private async Task ExtractExactMatchAsync(string fileName, IReadOnlyCollection<SearchResult> results)
    {
        var hit = results.First();

        if (results.Count > 1)
        {
            AppendLog(LogLevel.Warning, $"{results.Count} matches found for this filename; using: {hit.ZipName}");
        }

        AppendLog(LogLevel.Success, "TARGET LOCATED!");
        AppendLog(LogLevel.Info, $"Located in archive: {hit.ZipName}");

        await ExtractResultAsync(hit);
    }

    private async void ResultLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not SearchResultRow row)
            return;

        button.IsEnabled = false;

        try
        {
            AppendLog(LogLevel.Info, $"Extracting from Set Match results: {row.FileName}");

            await ExtractResultAsync(row.Result);
        }
        catch (Exception ex)
        {
            AppendLog(LogLevel.Error, $"Error: {ex.Message}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async Task ExtractResultAsync(SearchResult hit)
    {
        if (string.IsNullOrWhiteSpace(hit.ZipPath) || !File.Exists(hit.ZipPath))
        {
            AppendLog(LogLevel.Error, $"Indexed ZIP not found on disk: {hit.ZipPath} (index may be stale — try re-scanning).");
            return;
        }

        using var archive = ZipFile.OpenRead(hit.ZipPath);

        var entry = archive.GetEntry(hit.EntryPath);

        if (entry == null)
        {
            AppendLog(LogLevel.Error, "Entry not found inside ZIP (index may be stale — try re-scanning).");
            return;
        }

        Directory.CreateDirectory(_extractOutputPath);

        var outputPath = Path.Combine(_extractOutputPath, hit.FileName);

        await Task.Run(() => entry.ExtractToFile(outputPath, overwrite: true));

        AppendLog(LogLevel.Success, $"Output Path: {outputPath}");
    }

    private static string SanitizeFileName(string input)
    {
        var value = input.Trim();

        // Strip wrapping quotes some copy sources (e.g. "Copy as path" in Windows Explorer) add.
        if (value.Length >= 2 && value.StartsWith('"') && value.EndsWith('"'))
        {
            value = value[1..^1].Trim();
        }

        // If a full path got pasted in instead of just the filename, reduce to the filename.
        var lastSeparator = value.LastIndexOfAny(new[] { '\\', '/' });
        if (lastSeparator >= 0 && lastSeparator < value.Length - 1)
        {
            value = value[(lastSeparator + 1)..];
        }

        // Trailing punctuation/whitespace picked up from copy/paste (e.g. a sentence-ending period).
        value = value.TrimEnd('.', ' ', '\t', '\r', '\n');

        return value;
    }

    private void AppendLog(LogLevel level, string message)
    {
        var color = level switch
        {
            LogLevel.Success => SuccessColor,
            LogLevel.Warning => WarningColor,
            LogLevel.Error => ErrorColor,
            _ => InfoColor
        };

        _log.Add(new LogEntry
        {
            Display = $"[{DateTime.Now:HH:mm:ss}] {message}",
            Color = color
        });

        LogScrollViewer.ScrollToBottom();
    }

    private enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    private sealed class LogEntry
    {
        public string Display { get; init; } = string.Empty;
        public Brush Color { get; init; } = Brushes.White;
    }

    private sealed class SearchResultRow
    {
        public SearchResultRow(SearchResult result, bool isDuplicate)
        {
            Result = result;
            IsDuplicate = isDuplicate;
        }

        public SearchResult Result { get; }
        public bool IsDuplicate { get; }

        public string FileName => Result.FileName;
        public string ZipName => Result.ZipName;
        public DateTime StartTime => Result.StartTime;

        public Visibility DuplicateWarningVisibility => IsDuplicate ? Visibility.Visible : Visibility.Collapsed;

        public string LinkToolTip => IsDuplicate
            ? "Duplicate filename across archives - extracting this will overwrite any previous download of the same name."
            : "Click to extract this file";
    }

    private sealed class SearchUISettings
    {
        public string ExtractOutputPath { get; set; } = string.Empty;
    }
}
