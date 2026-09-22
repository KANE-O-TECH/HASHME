using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KaneO.HashMe.Core;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMenuItem = System.Windows.Controls.MenuItem;

namespace KaneO.HashMe.App;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush NewBrush = ThemePalette.Brush("#FF007FA3");
    private static readonly SolidColorBrush ExistingBrush = ThemePalette.Brush("#FFC86916");
    private static readonly SolidColorBrush SuccessBrush = ThemePalette.Brush("#FF16784F");
    private static readonly SolidColorBrush ChangedBrush = ThemePalette.Brush("#FFB3261E");
    private static readonly SolidColorBrush MismatchBrush = ThemePalette.Brush("#FFD13438");
    private static readonly SolidColorBrush MixedBrush = ThemePalette.Brush("#FF7156A5");
    private static readonly SolidColorBrush BusyBrush = ThemePalette.Brush("#FF275B80");
    private static readonly SolidColorBrush IdleBrush = ThemePalette.Brush("#FF334752");

    private readonly SettingsStore _settingsStore;
    private readonly HashWorkflow _workflow;
    private readonly DispatcherTimer _positionSaveTimer;
    private readonly List<HashResult> _results = [];
    private HashMeSettings _settings = new();
    private CancellationTokenSource? _hashCancellation;
    private PairHashResult? _lastPair;
    private int _operationGeneration;
    private bool _pairModeArmed;
    private bool _allowClose;
    private bool _initializing = true;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KANE-O",
            "HASHME");
        _settingsStore = new SettingsStore(Path.Combine(dataDirectory, "settings.json"));
        _workflow = new HashWorkflow(
            new FileHashCalculator(),
            new SidecarHashReader(),
            new HashCatalogStore(Path.Combine(dataDirectory, "hash-records.json")));

        _positionSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _positionSaveTimer.Tick += PositionSaveTimer_Tick;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsStore.LoadAsync();
        Topmost = _settings.AlwaysOnTop;
        ApplyTheme(_settings.Theme);
        RestorePosition();
        InitializeTrayIcon();
        _initializing = false;
        ShowIdle();
    }

    private void InitializeTrayIcon()
    {
        var icon = Environment.ProcessPath is { } executable
            ? System.Drawing.Icon.ExtractAssociatedIcon(executable)
            : null;

        var trayMenu = new System.Windows.Forms.ContextMenuStrip();
        trayMenu.Items.Add("Show HASHME", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        trayMenu.Items.Add("Exit", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Text = "KANE-O's HASHME v1.2",
            Visible = true,
            ContextMenuStrip = trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private async void Window_Drop(object sender, WpfDragEventArgs e)
    {
        e.Handled = true;
        ApplyTheme(_settings.Theme);
        if (!e.Data.GetDataPresent(WpfDataFormats.FileDrop) ||
            e.Data.GetData(WpfDataFormats.FileDrop) is not string[] droppedPaths)
        {
            ShowError("FILES ONLY", "Drop one or more files from Windows Explorer.");
            return;
        }

        var resolvedPaths = droppedPaths
            .Select(ShortcutResolver.Resolve)
            .Where(path => !Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (resolvedPaths.Length == 0)
        {
            ShowError("FILES ONLY", "Folders are not hashed. Drop one or more files.");
            return;
        }

        if (_pairModeArmed && resolvedPaths.Length != 2)
        {
            ShowError("PAIR NEEDS 2", "Pair Match is armed. Drop exactly two files together.");
            return;
        }

        _hashCancellation?.Cancel();
        _hashCancellation?.Dispose();
        _hashCancellation = new CancellationTokenSource();
        var cancellationToken = _hashCancellation.Token;
        var operationGeneration = ++_operationGeneration;

        _results.Clear();
        _lastPair = null;

        try
        {
            for (var index = 0; index < resolvedPaths.Length; index++)
            {
                var currentNumber = index + 1;
                var path = resolvedPaths[index];
                var progress = new Progress<HashProgress>(value =>
                {
                    if (operationGeneration == _operationGeneration)
                    {
                        ShowHashing(path, currentNumber, resolvedPaths.Length, value.Percent);
                    }
                });

                ShowHashing(path, currentNumber, resolvedPaths.Length, 0);
                var result = await _workflow.ProcessFileAsync(path, progress, cancellationToken);
                if (operationGeneration != _operationGeneration)
                {
                    return;
                }

                _results.Add(result);
            }

            if (_pairModeArmed)
            {
                _pairModeArmed = false;
                PairModeMenuItem.IsChecked = false;
                _lastPair = new PairHashResult(_results[0], _results[1]);
            }

            ShowResults();
        }
        catch (OperationCanceledException) when (operationGeneration != _operationGeneration)
        {
            // A newer drop or Clear command owns the visible state.
        }
        catch (OperationCanceledException)
        {
            ShowError("CANCELLED", "The previous hashing operation was cancelled.");
        }
    }

    private void ShowHashing(string path, int itemNumber, int totalItems, int percent)
    {
        StatusText.Text = totalItems == 1 ? "HASHING" : $"HASHING {itemNumber}/{totalItems}";
        StatusBadge.Background = BusyBrush;
        HashText.Text = $"{percent}%";
        HashText.ToolTip = $"Reading {Path.GetFileName(path)}\n{path}";
        CopyButton.IsEnabled = false;
    }

    private void ShowResults()
    {
        if (_results.Count == 0)
        {
            ShowIdle();
            return;
        }

        var displayValue = HashResultFormatter.FormatDisplay(_results);
        HashText.Text = string.IsNullOrWhiteSpace(displayValue) ? "NO HASH RESULT" : displayValue;
        HashText.ToolTip = string.Join(
            Environment.NewLine,
            _results.Select(result =>
                $"{result.DisplayName}: {result.Detail}" +
                (string.IsNullOrWhiteSpace(result.VerificationSource)
                    ? string.Empty
                    : $" ({result.VerificationSource})")));

        if (_lastPair is not null)
        {
            StatusText.Text = _lastPair.IsMatch ? "MATCH" : "DIFFERENT";
            StatusBadge.Background = _lastPair.IsMatch ? SuccessBrush : MismatchBrush;
        }
        else
        {
            SetAggregateStatus();
        }

        CopyButton.IsEnabled = _results.Any(result => result.IsSuccessful);
        CopyButton.ToolTip = _lastPair is null
            ? "Copy all file names and SHA-256 values"
            : "Copy both paired file names, SHA-256 values, and match result";
    }

    private void SetAggregateStatus()
    {
        var successful = _results.Where(result => result.IsSuccessful).ToArray();
        var mismatchCount = successful.Count(result => result.Status == HashResultStatus.Mismatch);
        var changedCount = successful.Count(result => result.Status == HashResultStatus.Changed);
        var newCount = successful.Count(result => result.Status == HashResultStatus.New);
        var existingCount = successful.Count(result => result.Status == HashResultStatus.Verified);
        var errorCount = _results.Count(result => result.Status == HashResultStatus.Error);

        if (errorCount > 0)
        {
            StatusText.Text = errorCount == 1 ? "ERROR" : $"{errorCount} ERRORS";
            StatusBadge.Background = MismatchBrush;
        }
        else if (mismatchCount > 0)
        {
            StatusText.Text = mismatchCount == 1 ? "MISMATCH" : $"{mismatchCount} MISMATCH";
            StatusBadge.Background = MismatchBrush;
        }
        else if (changedCount > 0)
        {
            StatusText.Text = changedCount == 1 ? "CHANGED" : $"{changedCount} CHANGED";
            StatusBadge.Background = ChangedBrush;
        }
        else if (newCount > 0 && existingCount > 0)
        {
            StatusText.Text = $"{newCount} NEW · {existingCount} EXISTING";
            StatusBadge.Background = MixedBrush;
        }
        else if (newCount > 0)
        {
            StatusText.Text = newCount == 1 ? "NEW" : $"{newCount} NEW";
            StatusBadge.Background = NewBrush;
        }
        else if (existingCount > 0)
        {
            StatusText.Text = existingCount == 1 ? "EXISTING" : $"{existingCount} EXISTING";
            StatusBadge.Background = ExistingBrush;
        }
    }

    private void ShowIdle()
    {
        StatusText.Text = _pairModeArmed ? "PAIR ARMED" : "READY";
        StatusBadge.Background = _pairModeArmed ? BusyBrush : IdleBrush;
        HashText.Text = _pairModeArmed ? "DROP EXACTLY 2 FILES ANYWHERE" : "DROP FILE(S) ANYWHERE";
        HashText.ToolTip = _pairModeArmed
            ? "The next drop must contain exactly two files."
            : "Drag one or more files onto any part of the HASHME window.";
        CopyButton.IsEnabled = false;
    }

    private void ShowError(string label, string detail)
    {
        StatusText.Text = label;
        StatusBadge.Background = MismatchBrush;
        HashText.Text = detail;
        HashText.ToolTip = detail;
        CopyButton.IsEnabled = false;
    }

    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count == 0)
        {
            return;
        }

        var value = _lastPair is not null
            ? HashResultFormatter.FormatPair(_lastPair)
            : HashResultFormatter.FormatStandard(_results);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (await SetClipboardTextAsync(value))
        {
            await FlashCopiedAsync();
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ++_operationGeneration;
        _hashCancellation?.Cancel();
        _results.Clear();
        _lastPair = null;
        _pairModeArmed = false;
        PairModeMenuItem.IsChecked = false;
        ShowIdle();
    }

    private async void ReplaceStoredRecord_Click(object sender, RoutedEventArgs e)
    {
        var changedIndices = _results
            .Select((result, index) => (result, index))
            .Where(item => item.result.Status == HashResultStatus.Changed)
            .ToArray();
        if (changedIndices.Length == 0)
        {
            return;
        }

        var noun = changedIndices.Length == 1 ? "record" : "records";
        var answer = WpfMessageBox.Show(
            $"Replace {changedIndices.Length} stored HASHME {noun} with the currently calculated SHA-256?\n\n" +
            "This changes HASHME's local verification baseline. The original files are not modified.",
            "Replace stored HASHME record",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        StatusText.Text = "RECHECKING";
        StatusBadge.Background = BusyBrush;
        CopyButton.IsEnabled = false;
        try
        {
            foreach (var item in changedIndices)
            {
                _results[item.index] = await _workflow.ReplaceStoredRecordAsync(item.result);
            }

            ShowResults();
        }
        catch (InvalidOperationException exception)
        {
            ShowError("NOT REPLACED", exception.Message);
        }
    }

    private static async Task<bool> SetClipboardTextAsync(string value)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                WpfClipboard.SetText(value, System.Windows.TextDataFormat.UnicodeText);
                return true;
            }
            catch (COMException) when (attempt < 4)
            {
                await Task.Delay(80 * (attempt + 1));
            }
        }

        return false;
    }

    private async Task FlashCopiedAsync()
    {
        var originalText = StatusText.Text;
        var originalBrush = StatusBadge.Background;
        StatusText.Text = "COPIED";
        StatusBadge.Background = SuccessBrush;
        await Task.Delay(900);
        StatusText.Text = originalText;
        StatusBadge.Background = originalBrush;
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Copy_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Escape)
        {
            Clear_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void Window_DragEnter(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(WpfDataFormats.FileDrop) ? WpfDragDropEffects.Copy : WpfDragDropEffects.None;
        RootBorder.BorderBrush = ThemePalette.Brush("#FF00C8FF");
        RootBorder.BorderThickness = new Thickness(2);
        e.Handled = true;
    }

    private void Window_DragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(WpfDataFormats.FileDrop)
            ? WpfDragDropEffects.Copy
            : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, WpfDragEventArgs e) => ApplyTheme(_settings.Theme);

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_settings.LockPosition || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (FindAncestor<WpfButton>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        DragMove();
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T found)
            {
                return found;
            }

            current = current switch
            {
                System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(current),
                FrameworkContentElement contentElement => contentElement.Parent,
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }

    private async void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = AlwaysOnTopMenuItem.IsChecked;
        Topmost = _settings.AlwaysOnTop;
        await SaveSettingsAsync();
    }

    private async void LockPosition_Click(object sender, RoutedEventArgs e)
    {
        _settings.LockPosition = LockPositionMenuItem.IsChecked;
        LockIndicator.Visibility = _settings.LockPosition ? Visibility.Visible : Visibility.Collapsed;
        await SaveSettingsAsync();
    }

    private void PairMode_Click(object sender, RoutedEventArgs e)
    {
        _pairModeArmed = PairModeMenuItem.IsChecked;
        _results.Clear();
        _lastPair = null;
        ShowIdle();
    }

    private async void Theme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfMenuItem { Tag: string themeName })
        {
            return;
        }

        _settings.Theme = themeName;
        ApplyTheme(themeName);
        await SaveSettingsAsync();
    }

    private void ApplyTheme(string? name)
    {
        var theme = ThemePalette.Get(name);
        var panelBrush = ThemePalette.Brush(theme.Panel);
        var fieldBrush = ThemePalette.Brush(theme.Field);
        var borderBrush = ThemePalette.Brush(theme.Border);
        var textBrush = ThemePalette.Brush(theme.Text);
        var mutedTextBrush = ThemePalette.Brush(theme.MutedText);
        var hoverBrush = ThemePalette.Brush(theme.Hover);

        WpfApplication.Current.Resources["PanelBrush"] = panelBrush;
        WpfApplication.Current.Resources["FieldBrush"] = fieldBrush;
        WpfApplication.Current.Resources["BorderBrush"] = borderBrush;
        WpfApplication.Current.Resources["TextBrush"] = textBrush;
        WpfApplication.Current.Resources["MutedTextBrush"] = mutedTextBrush;
        WpfApplication.Current.Resources["HoverBrush"] = hoverBrush;

        StripContextMenu.Resources[System.Windows.SystemColors.MenuBrushKey] = fieldBrush;
        StripContextMenu.Resources[System.Windows.SystemColors.MenuTextBrushKey] = textBrush;
        StripContextMenu.Resources[System.Windows.SystemColors.HighlightBrushKey] = hoverBrush;
        StripContextMenu.Resources[System.Windows.SystemColors.HighlightTextBrushKey] = textBrush;
        StripContextMenu.Resources[System.Windows.SystemColors.GrayTextBrushKey] = mutedTextBrush;
        RootBorder.BorderThickness = new Thickness(1);
        RootBorder.BorderBrush = borderBrush;
        StripContextMenu.Background = fieldBrush;
        StripContextMenu.Foreground = textBrush;
    }

    private async void StartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupRegistration.SetEnabled(StartWithWindowsMenuItem.IsChecked);
            _settings.StartWithWindows = StartWithWindowsMenuItem.IsChecked;
            await SaveSettingsAsync();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            StartWithWindowsMenuItem.IsChecked = _settings.StartWithWindows;
            WpfMessageBox.Show(exception.Message, "HASHME", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        AlwaysOnTopMenuItem.IsChecked = _settings.AlwaysOnTop;
        LockPositionMenuItem.IsChecked = _settings.LockPosition;
        PairModeMenuItem.IsChecked = _pairModeArmed;
        StartWithWindowsMenuItem.IsChecked = _settings.StartWithWindows;

        HashMeDarkThemeMenuItem.IsChecked = ThemePalette.NormalizeName(_settings.Theme) == ThemePalette.HashMeDarkName;
        GraphiteThemeMenuItem.IsChecked = _settings.Theme == ThemePalette.GraphiteName;
        LightThemeMenuItem.IsChecked = _settings.Theme == ThemePalette.LightName;
        HighContrastThemeMenuItem.IsChecked = _settings.Theme == ThemePalette.HighContrastName;

        var hasResults = _results.Count > 0;
        CopyMenuItem.IsEnabled = _results.Any(result => result.IsSuccessful);
        ClearMenuItem.IsEnabled = hasResults || _pairModeArmed;
        ReplaceStoredRecordMenuItem.IsEnabled = _results.Any(result => result.Status == HashResultStatus.Changed);
        ReplaceStoredRecordMenuItem.Header = _results.Count(result => result.Status == HashResultStatus.Changed) > 1
            ? "Replace changed stored records with current hashes…"
            : "Replace stored record with current hash…";
        OpenLocationMenuItem.IsEnabled = hasResults && File.Exists(_results[0].SourcePath);
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count == 0)
        {
            return;
        }

        var path = _results[0].SourcePath;
        if (!File.Exists(path))
        {
            ShowError("NOT FOUND", "The original file is no longer at its recorded location.");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path.Replace("\"", string.Empty, StringComparison.Ordinal)}\"",
            UseShellExecute = true
        });
    }

    private async void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        PositionAtTopRight();
        _settings.Left = Left;
        _settings.Top = Top;
        await SaveSettingsAsync();
    }

    private void RestorePosition()
    {
        if (_settings.Left is { } left && _settings.Top is { } top && PositionIsVisible(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            PositionAtTopRight();
        }

        LockIndicator.Visibility = _settings.LockPosition ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PositionAtTopRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 24;
        Top = workArea.Top + 24;
    }

    private bool PositionIsVisible(double left, double top)
    {
        var right = left + Width;
        var bottom = top + Height;
        var virtualRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

        return right > SystemParameters.VirtualScreenLeft + 24 &&
               left < virtualRight - 24 &&
               bottom > SystemParameters.VirtualScreenTop + 18 &&
               top < virtualBottom - 18;
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        _positionSaveTimer.Stop();
        _positionSaveTimer.Start();
    }

    private async void PositionSaveTimer_Tick(object? sender, EventArgs e)
    {
        _positionSaveTimer.Stop();
        _settings.Left = Left;
        _settings.Top = Top;
        await SaveSettingsAsync();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsStore.SaveAsync(_settings);
        }
        catch (IOException)
        {
            // A settings write failure must never block hashing or clipboard use.
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = _settings.AlwaysOnTop;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        WpfMessageBox.Show(
            "KANE-O's HASHME v1.2\n\n" +
            "A lightweight Windows 11+ SHA-256 utility.\n" +
            "Drop files. Verify evidence. Copy instantly.\n\n" +
            "Files are read, never rewritten. HASHME records fingerprints locally.\n" +
            "Changed records require explicit approval before replacement.\n\n" +
            "Copyright © 2026 KANE-O, trading as KANE-O-TECH.\n" +
            "All rights reserved except as expressly permitted by the applicable licence.",
            "About HASHME",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void ExitApplication()
    {
        _allowClose = true;
        _hashCancellation?.Cancel();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        Close();
        WpfApplication.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
