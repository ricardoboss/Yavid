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
    public static readonly DependencyProperty PlaylistUrlProperty =
        DependencyProperty.Register(nameof(PlaylistUrl), typeof(string), typeof(MainWindow), new(string.Empty));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(MainWindow), new(string.Empty));

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        WriteStatus("Ready.");
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

    private async Task DoDownload()
    {
        if (!VerifyCanRun()) return;

        var toolsReader = PrepareTools(out var youtubeDlPath, out var ffmpegPath);
        if (!toolsReader) return;

        var outputFolder = AskForOutputFolder();
        if (outputFolder is null)
        {
            WriteStatus("No output folder selected. Aborting.");

            return;
        }

        var success = await InvokeYoutubeDl(youtubeDlPath, ffmpegPath, outputFolder);
        if (!success)
        {
            WriteStatus("youtube-dl invocation failed. Aborting.");

            return;
        }

        OpenResultFolder(outputFolder);

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

        var command = Cli.Wrap(youtubeDlPath)
            .WithArguments([
                "--extract-audio",
                "--audio-format", "mp3",
                "--ffmpeg-location", ffmpegPath,
                "--output", Path.Combine(outputFolder, "%(title)s.%(ext)s"),
                "--embed-thumbnail",
                PlaylistUrl,
            ])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => WriteStatus(s, "youtube-dl")))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => WriteStatus(s, "youtube-dl")));

        WriteStatus("Running command: " + command);

        var result = await command.ExecuteAsync();

        return result.IsSuccess;
    }

    [SuppressMessage("ReSharper", "InvertIf")]
    private bool VerifyCanRun()
    {
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