using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using CliWrap;
using Microsoft.Win32;

namespace Yavid;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    public static readonly DependencyProperty OutputFolderProperty =
        DependencyProperty.Register(nameof(OutputFolder), typeof(string), typeof(MainWindow),
            new(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), OutputFolderChanged));

    public static readonly DependencyProperty OpenOutputAfterDownloadProperty =
        DependencyProperty.Register(nameof(OpenOutputAfterDownload), typeof(bool), typeof(MainWindow),
            new(true, OpenOutputAfterDownloadChanged));

    public static readonly DependencyProperty PlaylistUrlProperty =
        DependencyProperty.Register(nameof(PlaylistUrl), typeof(string), typeof(MainWindow), new(string.Empty));

    public static readonly DependencyProperty IncludeThumbnailsProperty =
        DependencyProperty.Register(nameof(IncludeThumbnails), typeof(bool), typeof(MainWindow),
            new(true, IncludeThumbnailsChanged));

    public static readonly DependencyProperty OnlyKeepAudioProperty =
        DependencyProperty.Register(nameof(OnlyKeepAudio), typeof(bool), typeof(MainWindow),
            new(true, OnlyKeepAudioChanged));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(MainWindow), new(string.Empty));

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        OutputFolder = GetFromRegistry(nameof(OutputFolder),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop))!;
        OpenOutputAfterDownload = GetFromRegistry(nameof(OpenOutputAfterDownload), bool.TrueString)! == bool.TrueString;
        IncludeThumbnails = GetFromRegistry(nameof(IncludeThumbnails), bool.TrueString)! == bool.TrueString;
        OnlyKeepAudio = GetFromRegistry(nameof(OnlyKeepAudio), bool.TrueString)! == bool.TrueString;

        WriteStatus("Ready.");
    }

    private static string? GetFromRegistry(string propertyName, string? defaultValue = null)
    {
        var key = Registry.CurrentUser.OpenSubKey(@"Software\Yavid");

        return key?.GetValue(propertyName, defaultValue) as string;
    }

    private static void SetInRegistry(string propertyName, string value)
    {
        var key = Registry.CurrentUser.CreateSubKey(@"Software\Yavid");

        key.SetValue(propertyName, value, RegistryValueKind.String);
    }

    private static void SetInRegistry(string propertyName, bool value)
    {
        var key = Registry.CurrentUser.CreateSubKey(@"Software\Yavid");

        key.SetValue(propertyName, value ? bool.TrueString : bool.FalseString, RegistryValueKind.String);
    }

    public string OutputFolder
    {
        get => (string)GetValue(OutputFolderProperty)!;
        set => SetValue(OutputFolderProperty, value);
    }

    private static void OutputFolderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newValue = (string)e.NewValue!;

        SetInRegistry(nameof(OutputFolder), newValue);
    }

    public bool OpenOutputAfterDownload
    {
        get => (bool)GetValue(OpenOutputAfterDownloadProperty)!;
        set => SetValue(OpenOutputAfterDownloadProperty, value);
    }

    private static void OpenOutputAfterDownloadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newValue = (bool)e.NewValue!;

        SetInRegistry(nameof(OpenOutputAfterDownload), newValue);
    }

    public bool IncludeThumbnails
    {
        get => (bool)GetValue(IncludeThumbnailsProperty)!;
        set => SetValue(IncludeThumbnailsProperty, value);
    }

    private static void IncludeThumbnailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newValue = (bool)e.NewValue!;

        SetInRegistry(nameof(IncludeThumbnails), newValue);
    }

    public bool OnlyKeepAudio
    {
        get => (bool)GetValue(OnlyKeepAudioProperty)!;
        set => SetValue(OnlyKeepAudioProperty, value);
    }

    private static void OnlyKeepAudioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newValue = (bool)e.NewValue!;

        SetInRegistry(nameof(OnlyKeepAudio), newValue);
    }

    public string PlaylistUrl
    {
        get => (string)GetValue(PlaylistUrlProperty)!;
        set => SetValue(PlaylistUrlProperty, value);
    }

    public string Status
    {
        get => (string)GetValue(StatusProperty)!;
        set => SetValue(StatusProperty, value);
    }

    private void WriteStatus(string status, string topic = "")
    {
        status = $"[{DateTime.Now:HH:mm:ss}] {(string.IsNullOrEmpty(topic) ? "" : $"[{topic}] ")}{status}";

        Dispatcher?.Invoke(() => Status = status + Environment.NewLine + Status);
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        _ = DoDownload();
    }

    private void ChooseOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        OutputFolder = AskForOutputFolder() ?? string.Empty;
    }

    private async Task DoDownload()
    {
        if (!VerifyCanRun()) return;

        var toolsReader = PrepareTools(out var youtubeDlPath, out var ffmpegPath);
        if (!toolsReader) return;

        var success = await InvokeYoutubeDl(youtubeDlPath, ffmpegPath, OutputFolder);
        if (!success)
        {
            WriteStatus("youtube-dl invocation failed. Aborting.");

            return;
        }

        if (OpenOutputAfterDownload)
            OpenOutputFolder(OutputFolder);

        WriteStatus("Done!");
    }

    private string? AskForOutputFolder()
    {
        WriteStatus("Asking for output folder...");

        var dialog = new OpenFolderDialog
        {
            Title = "Select output folder",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };

        return dialog.ShowDialog() != true ? null : dialog.FolderName;
    }

    private void OpenOutputFolder(string outputFolder)
    {
        WriteStatus("Opening output folder...");

        Process.Start(new ProcessStartInfo
        {
            FileName = outputFolder,
            UseShellExecute = true,
        });
    }

    private async Task<bool> InvokeYoutubeDl(string youtubeDlPath, string ffmpegPath, string outputFolder)
    {
        WriteStatus("Invoking youtube-dl...");

        List<string> arguments =
        [
            "--ffmpeg-location", ffmpegPath,
            "--output", Path.Combine(outputFolder, "%(title)s.%(ext)s"),
        ];

        if (OnlyKeepAudio)
        {
            arguments.AddRange([
                "--extract-audio",
                "--audio-format", "mp3",
            ]);
        }
        else
        {
            arguments.AddRange([
                // "--format", "best", // Apparently this does in fact _not_ choose the best format
                "--embed-metadata",
            ]);
        }

        if (IncludeThumbnails)
            arguments.Add("--embed-thumbnail");

        arguments.Add(PlaylistUrl);

        var command = Cli.Wrap(youtubeDlPath)
            .WithArguments(arguments)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => WriteStatus(s, "youtube-dl")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => WriteStatus(s, "youtube-dl")));

        WriteStatus("Running command: " + command);

        var result = await command.ExecuteAsync();

        return result.IsSuccess;
    }

    [SuppressMessage("ReSharper", "InvertIf")]
    private bool VerifyCanRun()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            WriteStatus("Please select an output folder");

            return false;
        }

        if (string.IsNullOrWhiteSpace(PlaylistUrl))
        {
            WriteStatus("Please enter a video/playlist URL");

            return false;
        }

        return true;
    }

    private bool PrepareTools(out string youtubeDlPath, out string ffmpegPath)
    {
        WriteStatus("Preparing tools...");

        youtubeDlPath = Path.Combine(Environment.CurrentDirectory, "assets", "yt-dlp.exe");
        ffmpegPath = Path.Combine(Environment.CurrentDirectory, "assets", "ffmpeg.exe");

        if (!File.Exists(youtubeDlPath))
        {
            WriteStatus($"youtube-dl not found at '{youtubeDlPath}'. Aborting.");

            return false;
        }

        if (!File.Exists(ffmpegPath))
        {
            WriteStatus($"ffmpeg not found at '{ffmpegPath}'. Aborting.");

            return false;
        }

        WriteStatus("Tools ready.");

        return true;
    }
}
