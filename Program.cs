// Copyright Karich Design 2024
// Remember to install xclip, yt-dlp and ffmpeg!!!!!

using System.Diagnostics;
using System.Text.RegularExpressions;
using Gtk;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;
using Application = Gtk.Application;

class Program
{
	static void Main(string[] args)
	{
		Application.Init();
		var app = new Application("org.KarichDesign.YoutubeDownloader", GLib.ApplicationFlags.None);
		app.Register(GLib.Cancellable.Current);

		var win = new MainWindow();
		app.AddWindow(win);

		win.ShowAll();
		Application.Run();
	}
}

class MainWindow : Window
{
	private TreeView downloadsView;
	private ListStore downloadsListStore;
	private Queue<DownloadItem> downloadQueue = new Queue<DownloadItem>();
	private bool isDownloading = false;

	public MainWindow() : base("YouTube Downloader v1.0")
	{
		SetDefaultSize(400, 200);
		SetPosition(WindowPosition.Center);
		DeleteEvent += (sender, e) => Application.Quit();

		SetupDownloadsList();
		StartMonitoring();
	}

	private void SetupDownloadsList()
	{
		downloadsListStore = new ListStore(typeof(string), typeof(string));
		downloadsView = new TreeView(downloadsListStore);

		var urlColumn = new TreeViewColumn("URL", new CellRendererText(), "text", 0);
		downloadsView.AppendColumn(urlColumn);

		var statusColumn = new TreeViewColumn("Status", new CellRendererText(), "text", 1);
		downloadsView.AppendColumn(statusColumn);

		Add(downloadsView);
	}

	public async void StartMonitoring()
	{
		string lastClipboardText = "";
		while (true)
		{
			string text = await Task.Run(() => GetClipboardText());
			if (!string.IsNullOrWhiteSpace(text) && text != lastClipboardText && IsValidYoutubeUrl(text))
			{
				lastClipboardText = text;
				QueueDownload(text);
			}
			Thread.Sleep(1000);
		}
	}

	private string GetClipboardText()
	{
		var psi = new ProcessStartInfo("xclip", "-selection clipboard -o")
		{
			RedirectStandardOutput = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		var process = Process.Start(psi);
		string text = process.StandardOutput.ReadToEnd();
		process.WaitForExit();
		return text;
	}

	private bool IsValidYoutubeUrl(string url)
	{
		return Regex.IsMatch(url, @"(https?:\/\/)?(www\.)?(youtube\.com|youtu\.?be)\/.+");
	}

	private async void QueueDownload(string url)
	{
		var ytdl = new YoutubeDL();
		var videoInfo = await ytdl.RunVideoDataFetch(url);
		if (videoInfo.Success)
		{
			string title = videoInfo.Data.Title ?? "Unknown Title";
			var downloadItem = new DownloadItem(url, title);
			Application.Invoke((sender, e) =>
			{
				var iter = downloadsListStore.AppendValues(downloadItem.Title, downloadItem.Status); // Use title
				downloadItem.TreeIter = iter;
			});
			downloadQueue.Enqueue(downloadItem);

			if (!isDownloading)
			{
				Task.Run(() => ProcessDownloadQueue());
			}
		}
		else
		{
			Console.WriteLine("Failed to fetch video information.");
		}
	}

	private async Task ProcessDownloadQueue()
	{
		while (downloadQueue.Count > 0)
		{
			if (!isDownloading)
			{
				var currentItem = downloadQueue.Dequeue();
				isDownloading = true;
				Application.Invoke((sender, e) => downloadsListStore.SetValue(currentItem.TreeIter, 1, "Downloading"));
				await DownloadYoutubeVideo(currentItem);
				isDownloading = false;
			}
		}
	}

	private async Task DownloadYoutubeVideo(DownloadItem item)
	{
		var ytdl = new YoutubeDL();
		var result = await ytdl.RunAudioDownload(item.Url, AudioConversionFormat.Mp3);

		Application.Invoke((sender, e) =>
		{
			if (result.Success)
			{
				downloadsListStore.SetValue(item.TreeIter, 1, "Completed");
				CleanAndRenameFile(result.Data);
			}
			else
			{
				downloadsListStore.SetValue(item.TreeIter, 1, "Failed");
			}
		});
	}

	private void CleanAndRenameFile(string originalFilePath)
	{
		var directory = System.IO.Path.GetDirectoryName(originalFilePath);
		var originalFileName = System.IO.Path.GetFileName(originalFilePath);

		// Regex to find text within () and [], including the () and [] themselves
		string pattern = @"[\[\(].*?[\]\)]";
		var cleanFileName = Regex.Replace(originalFileName, pattern, "");

		// Construct the new file path
		var newFilePath = System.IO.Path.Combine(directory, cleanFileName);

		// Rename the file
		if (originalFilePath != newFilePath)
		{
			// Ensure the new file name does not already exist to avoid overwriting
			if (File.Exists(newFilePath))
			{
				Console.WriteLine($"File {newFilePath} already exists. Choose a different name.");
				// Handle the case where the new file name already exists (e.g., append a number)
			}
			else
			{
				File.Move(originalFilePath, newFilePath);
				Console.WriteLine($"File renamed to {newFilePath}");
			}
		}
	}
}

class DownloadItem
{
	public string Url { get; set; }
	public string Title { get; set; } // Add title property
	public string Status { get; set; }
	public TreeIter TreeIter { get; set; }

	public DownloadItem(string url, string title) // Include title in constructor
	{
		Url = url;
		Title = title;
		Status = "Queued";
	}
}
