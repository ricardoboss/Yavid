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
        DependencyProperty.Register(nameof(OutputFolder), typeof(string), typeof(MainWindow), new(string.Empty));

    public static readonly DependencyProperty OpenOutputAfterDownloadProperty =
        DependencyProperty.Register(nameof(OpenOutputAfterDownload), typeof(bool), typeof(MainWindow), new(true));

    public static readonly DependencyProperty PlaylistUrlProperty =
        DependencyProperty.Register(nameof(PlaylistUrl), typeof(string), typeof(MainWindow), new(string.Empty));

    public static readonly DependencyProperty IncludeThumbnailsProperty =
        DependencyProperty.Register(nameof(IncludeThumbnails), typeof(bool), typeof(MainWindow), new(true));

    public static readonly DependencyProperty OnlyKeepAudioProperty =
        DependencyProperty.Register(nameof(OnlyKeepAudio), typeof(bool), typeof(MainWindow), new(true));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(MainWindow), new(string.Empty));

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        WriteStatus("Ready.");
    }

    public string OutputFolder
    {
        get => (string)GetValue(OutputFolderProperty)!;
        set => SetValue(OutputFolderProperty, value);
    }

    public bool OpenOutputAfterDownload
    {
        get => (bool)GetValue(OpenOutputAfterDownloadProperty)!;
        set => SetValue(OpenOutputAfterDownloadProperty, value);
    }

    public bool IncludeThumbnails
    {
        get => (bool)GetValue(IncludeThumbnailsProperty)!;
        set => SetValue(IncludeThumbnailsProperty, value);
    }

    public bool OnlyKeepAudio
    {
        get => (bool)GetValue(OnlyKeepAudioProperty)!;
        set => SetValue(OnlyKeepAudioProperty, value);
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
            OpenResultFolder(OutputFolder);

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

    private void OpenResultFolder(string outputFolder)
    {
        WriteStatus("Opening result folder...");

        Process.Start(new ProcessStartInfo
        {
            FileName = outputFolder,
            UseShellExecute = true,
        });
    }

    private async Task<bool> InvokeYoutubeDl(string youtubeDlPath, string ffmpegPath, string outputFolder)
    {
        WriteStatus("Invoking youtube-dl...");

        List<string> arguments = [
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
                "--format", "best",
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
            WriteStatus("Please enter a playlist URL");

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