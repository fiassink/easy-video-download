using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Launcher;

public sealed class Form1 : Form
{
	private sealed record VideoFormat(string Id, int Width, int Height, string Label, string SourceUrl, string Referer, string CookieHeader, string PlaylistItems = "");
	private static readonly HttpClient ThumbnailClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };

	private readonly TextBox urlBox = new TextBox
	{
		PlaceholderText = UiText.Get("Cole aqui a URL do vídeo", "Paste the video URL here"),
		Dock = DockStyle.Fill
	};

	private readonly ComboBox qualities = new ComboBox
	{
		DropDownStyle = ComboBoxStyle.DropDownList,
		Width = 300,
		Enabled = false
	};

	private readonly TextBox folderBox = new TextBox
	{
		ReadOnly = true,
		Dock = DockStyle.Fill
	};

	private readonly Button inspectButton = new Button
	{
		Text = UiText.Get("Ver qualidades", "Check qualities"),
		AutoSize = false,
		Size = new Size(126, 38)
	};

	private readonly Button downloadButton = new Button
	{
		Text = UiText.Get("Baixar vídeo", "Download video"),
		AutoSize = false,
		Size = new Size(126, 38),
		Enabled = false
	};

	private readonly Button cancelButton = new Button
	{
		Text = UiText.Get("Cancelar", "Cancel"),
		AutoSize = false,
		Size = new Size(126, 38),
		Enabled = false
	};

	private readonly Button folderButton = new Button
	{
		Text = UiText.Get("Escolher pasta…", "Choose folder…"),
		AutoSize = false,
		Size = new Size(124, 28),
		Margin = new Padding(8, 0, 0, 0)
	};

	private readonly Panel progressTrack = new Panel
	{
		Dock = DockStyle.Fill,
		Height = 14,
		BackColor = Color.FromArgb(226, 226, 226)
	};

	private readonly Panel progressFill = new Panel
	{
		Dock = DockStyle.Left,
		Width = 0,
		BackColor = Color.FromArgb(22, 132, 40)
	};

	private readonly Label status = new Label
	{
		Text = UiText.Get("Cole uma URL para começar.", "Paste a URL to get started."),
		AutoSize = false,
		Dock = DockStyle.Fill,
		Height = 24,
		AutoEllipsis = true,
		ForeColor = Color.DimGray
	};

	private readonly PictureBox thumbnailImage = new PictureBox
	{
		Dock = DockStyle.Fill,
		SizeMode = PictureBoxSizeMode.Zoom,
		BackColor = Color.FromArgb(232, 236, 242),
		BorderStyle = BorderStyle.FixedSingle,
		Visible = false
	};

	private readonly Label thumbnailPlaceholder = new Label
	{
		Text = UiText.Get("A prévia aparecerá após a consulta", "Preview appears after checking"),
		Dock = DockStyle.Fill,
		TextAlign = ContentAlignment.MiddleCenter,
		ForeColor = Color.DimGray,
		BackColor = Color.FromArgb(232, 236, 242)
	};

	private readonly Label previewTitle = new Label
	{
		Text = UiText.Get("Nenhum vídeo consultado", "No video checked"),
		AutoSize = true,
		MaximumSize = new Size(480, 0),
		Font = new Font("Segoe UI", 11f, FontStyle.Bold),
		ForeColor = Color.FromArgb(24, 35, 58)
	};

	private readonly Label previewDetails = new Label
	{
		Text = UiText.Get("Cole uma URL e clique em Ver qualidades.", "Paste a URL and click Check qualities."),
		AutoSize = true,
		MaximumSize = new Size(480, 0),
		ForeColor = Color.DimGray
	};

	private List<VideoFormat> availableFormats = new List<VideoFormat>();

	private int progressValue;

	private CancellationTokenSource? thumbnailLoadCts;
	private CancellationTokenSource? activeOperationCts;

	private string YtDlp => Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe");

	public Form1()
	{
		Text = "Easy Video Downloader";
		MinimumSize = new Size(680, 430);
		base.Size = new Size(780, 500);
		base.StartPosition = FormStartPosition.CenterScreen;
		Font = new Font("Segoe UI", 10f);
		BackColor = Color.FromArgb(248, 249, 252);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(28),
			ColumnCount = 1,
			RowCount = 11
		};
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 128f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 14f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = "Easy Video Downloader",
			AutoSize = true,
			Font = new Font("Segoe UI", 20f, FontStyle.Bold)
		}, 0, 0);
		tableLayoutPanel.Controls.Add(new Label
		{
			Text = UiText.Get("Cole o link, escolha a qualidade e baixe direto por aqui.", "Paste a link, choose a quality, and download it here."),
			AutoSize = true,
			ForeColor = Color.DimGray
		}, 0, 2);
		tableLayoutPanel.Controls.Add(urlBox, 0, 3);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			Padding = new Padding(0, 8, 0, 4),
			WrapContents = false
		};
		StyleButton(inspectButton);
		StyleButton(downloadButton);
		StyleButton(cancelButton);
		flowLayoutPanel.Controls.Add(inspectButton);
		flowLayoutPanel.Controls.Add(downloadButton);
		flowLayoutPanel.Controls.Add(cancelButton);
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 4);
		TableLayoutPanel qualityRow = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			ColumnCount = 2,
			RowCount = 1
		};
		qualityRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		qualityRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		qualityRow.Controls.Add(new Label
		{
			Text = UiText.Get("Qualidade:", "Quality:"),
			AutoSize = true,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = new Padding(0, 5, 12, 0)
		}, 0, 0);
		qualities.Dock = DockStyle.Fill;
		qualities.Margin = new Padding(0);
		qualityRow.Controls.Add(qualities, 1, 0);
		tableLayoutPanel.Controls.Add(qualityRow, 0, 5);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			AutoSize = false,
			Height = 28
		};
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
		tableLayoutPanel2.Controls.Add(folderBox, 0, 0);
		tableLayoutPanel2.Controls.Add(folderButton, 1, 0);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 6);
		TableLayoutPanel previewLayout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			Padding = new Padding(0, 8, 0, 8)
		};
		previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184f));
		previewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Panel thumbnailFrame = new Panel { Size = new Size(184, 104), Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(0, 0, 16, 0), MinimumSize = new Size(160, 90) };
		thumbnailFrame.Controls.Add(thumbnailImage);
		thumbnailFrame.Controls.Add(thumbnailPlaceholder);
		FlowLayoutPanel previewText = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true,
			Padding = new Padding(0, 8, 0, 0)
		};
		previewText.Controls.Add(previewTitle);
		previewText.Controls.Add(previewDetails);
		previewLayout.Controls.Add(thumbnailFrame, 0, 0);
		previewLayout.Controls.Add(previewText, 1, 0);
		progressTrack.Controls.Add(progressFill);
		tableLayoutPanel.Controls.Add(previewLayout, 0, 7);
		tableLayoutPanel.Controls.Add(progressTrack, 0, 9);
		tableLayoutPanel.Controls.Add(status, 0, 10);
		base.Controls.Add(tableLayoutPanel);
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		folderBox.Text = (Directory.Exists(text) ? text : AppContext.BaseDirectory);
		progressTrack.Resize += delegate
		{
			SetProgress(progressValue);
		};
		inspectButton.Click += async delegate
		{
			await InspectAsync();
		};
		downloadButton.Click += async delegate
		{
			await DownloadAsync();
		};
		folderButton.Click += delegate
		{
			ChooseFolder();
		};
		cancelButton.Click += delegate
		{
			if (activeOperationCts != null)
			{
				cancelButton.Enabled = false;
				SetStatus(UiText.Get("Cancelando a operação…", "Canceling operation…"));
				activeOperationCts.Cancel();
			}
		};
		urlBox.TextChanged += delegate
		{
			availableFormats = new List<VideoFormat>();
			qualities.DataSource = null;
			qualities.Enabled = false;
			downloadButton.Enabled = false;
			SetVideoPreview(UiText.Get("Nenhum vídeo consultado", "No video checked"), UiText.Get("Cole uma URL e clique em Ver qualidades.", "Paste a URL and click Check qualities."), UiText.Get("A prévia aparecerá após a consulta", "Preview appears after checking"));
			SetStatus(UiText.Get("URL alterada. Consulte novamente para ver as opções disponíveis.", "URL changed. Check it again to load available options."));
		};
	}

	private static void StyleButton(Button button)
	{
		button.BackColor = Color.White;
		button.ForeColor = Color.FromArgb(48, 57, 72);
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderColor = Color.FromArgb(205, 211, 220);
		button.FlatAppearance.BorderSize = 1;
		button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 244, 248);
		button.FlatAppearance.MouseDownBackColor = Color.FromArgb(229, 234, 241);
		button.Padding = new Padding(8, 4, 8, 4);
		button.Cursor = Cursors.Hand;
	}

	private void ChooseFolder()
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
		{
			SelectedPath = folderBox.Text,
			Description = UiText.Get("Escolha onde salvar os vídeos", "Choose where to save videos")
		};
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			folderBox.Text = folderBrowserDialog.SelectedPath;
		}
	}

	private async Task InspectAsync()
	{
		if (!TryGetUrl(out string url) || !CheckTools())
		{
			return;
		}
		availableFormats = new List<VideoFormat>();
		qualities.DataSource = null;
		qualities.Enabled = false;
		downloadButton.Enabled = false;
		SetProgress(0);
		SetVideoPreview(UiText.Get("Consultando vídeo…", "Checking video…"), UiText.Get("Buscando título, duração e thumbnail.", "Looking for title, duration, and thumbnail."), null);
		using CancellationTokenSource operationCts = new CancellationTokenSource();
		activeOperationCts = operationCts;
		SetBusy(value: true);
		bool browserFallbackAttempted = false;
		SetStatus(UiText.Get("Lendo as opções de qualidade…", "Loading quality options…"));
		try
		{
			List<string> args = new List<string> { "--dump-single-json", "--no-warnings", "--no-playlist", "--cookies-from-browser", "chrome", url };
			(int, string, string) tuple = await RunToolWithCookieFallbackAsync(args, captureOutput: true, cancellationToken: operationCts.Token);
			if (tuple.Item1 != 0)
			{
				if (IsPiracyBlocked(tuple.Item3))
				{
					SetStatus(ShortError(tuple.Item3), error: true);
					return;
				}
				browserFallbackAttempted = true;
				await CaptureMediaInBackgroundAsync(url, operationCts.Token);
				return;
			}
			using JsonDocument document = JsonDocument.Parse(tuple.Item2);
			JsonElement jsonElement = document.RootElement;
			string playlistItems = "";
			int playlistEntryCount = 0;
			int uniquePlaylistEntryCount = 0;
			if (jsonElement.TryGetProperty("entries", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				playlistItems = GetUniquePlaylistItems(value, out playlistEntryCount, out uniquePlaylistEntryCount);
				jsonElement = value.EnumerateArray().FirstOrDefault(entry => entry.ValueKind == JsonValueKind.Object);
			}
			availableFormats = ParseFormats(jsonElement, url, "", playlistItems: playlistItems);
			availableFormats = availableFormats
				.OrderByDescending(f => (long)f.Width * f.Height)
				.ThenByDescending(f => Math.Max(f.Width, f.Height))
				.ThenBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
				.ToList();
			if (availableFormats.Count == 0)
			{
				browserFallbackAttempted = true;
				await CaptureMediaInBackgroundAsync(url, operationCts.Token);
				return;
			}
			qualities.DataSource = availableFormats;
			qualities.DisplayMember = "Label";
			qualities.SelectedIndex = 0;
			qualities.Enabled = true;
			downloadButton.Enabled = true;
			string value2 = GetString(jsonElement, "title");
			UpdateVideoPreview(jsonElement, availableFormats.Count);
			if (playlistEntryCount > 1)
			{
				int duplicatesRemoved = playlistEntryCount - uniquePlaylistEntryCount;
				SetStatus(PortugueseAlbumStatus(uniquePlaylistEntryCount, playlistEntryCount, duplicatesRemoved));
			}
			else
			{
				SetStatus(string.IsNullOrWhiteSpace(value2)
					? UiText.Get($"{availableFormats.Count} qualidades encontradas.", $"{availableFormats.Count} qualities found.")
					: UiText.Get($"{value2} — {availableFormats.Count} qualidades disponíveis.", $"{value2} — {availableFormats.Count} qualities available."));
			}
		}
		catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
		{
			SetStatus(UiText.Get("Consulta cancelada.", "Check canceled."));
		}
		catch (Exception ex)
		{
			if (!browserFallbackAttempted)
			{
				try
				{
					await CaptureMediaInBackgroundAsync(url, operationCts.Token);
				}
				catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
				{
					SetStatus(UiText.Get("Consulta cancelada.", "Check canceled."));
				}
				catch (Exception ex2)
				{
					SetStatus(ex2.Message, error: true);
				}
			}
			else
			{
				SetStatus(ex.Message, error: true);
			}
		}
		finally
		{
			if (ReferenceEquals(activeOperationCts, operationCts))
			{
				activeOperationCts = null;
			}
			SetBusy(value: false);
		}
	}

	private async Task CaptureMediaInBackgroundAsync(string pageUrl, CancellationToken cancellationToken)
	{
		SetStatus(UiText.Get("Tentando localizar a mídia no player em uma janela invisível…", "Looking for media in the player…"));
		HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, string> cookiesByMediaUrl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		string capturedTitle = "";
		string capturedThumbnail = "";
		using Form hiddenWindow = new Form
		{
			ShowInTaskbar = false,
			Opacity = 0.01,
			StartPosition = FormStartPosition.Manual,
			Location = new Point(-3000, -3000),
			Size = new Size(800, 600),
			FormBorderStyle = FormBorderStyle.None
		};
		using WebView2 web = new WebView2
		{
			Dock = DockStyle.Fill
		};
		hiddenWindow.Controls.Add(web);
		hiddenWindow.Show();
		try
		{
			string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoDownloader", "WebView2");
			web.CreationProperties = new CoreWebView2CreationProperties
			{
				UserDataFolder = userDataFolder
			};
			await web.EnsureCoreWebView2Async();
			web.CoreWebView2.WebResourceResponseReceived += delegate(object? _, CoreWebView2WebResourceResponseReceivedEventArgs e)
			{
				string uri = e.Request.Uri;
				string text2 = "";
				try
				{
					text2 = e.Response.Headers.GetHeader("Content-Type");
				}
				catch
				{
				}
				if ((LooksLikeMedia(uri) || text2.Contains("video/mp4", StringComparison.OrdinalIgnoreCase) || text2.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) || text2.Contains("dash+xml", StringComparison.OrdinalIgnoreCase)) && Uri.TryCreate(uri, UriKind.Absolute, out Uri? result3) && result3 is not null && (result3.Scheme == "http" || result3.Scheme == "https"))
				{
					candidates.Add(uri);
				}
			};
			web.CoreWebView2.Navigate(pageUrl);
			for (int attempt = 0; attempt < 50; attempt++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				await Task.Delay(1000, cancellationToken);
				try
				{
					string javaScript = "(() => { const out=new Set(); for(const v of document.querySelectorAll('video')) { if(v.currentSrc && /^https?:/i.test(v.currentSrc)) out.add(v.currentSrc); if(v.src && /^https?:/i.test(v.src)) out.add(v.src); } for(const e of performance.getEntriesByType('resource')) if(/\\.(mp4|m3u8|mpd)(?:$|[?#])/i.test(e.name)) out.add(e.name); const rawImage=document.querySelector('meta[property=\"og:image:secure_url\"]')?.content||document.querySelector('meta[property=\"og:image\"]')?.content||document.querySelector('meta[name=\"twitter:image\"]')?.content||document.querySelector('video')?.poster||''; let image=''; try { image=rawImage?new URL(rawImage,location.href).href:''; } catch {} for(const v of document.querySelectorAll('video')) { v.muted=true; if(v.paused) v.play().catch(()=>{}); } return JSON.stringify({mediaUrls:[...out],title:document.title,thumbnail:image}); })()";
					string? text = JsonSerializer.Deserialize<string>(await web.CoreWebView2.ExecuteScriptAsync(javaScript));
					if (!string.IsNullOrWhiteSpace(text))
					{
						using JsonDocument jsonDocument = JsonDocument.Parse(text);
						JsonElement snapshot = jsonDocument.RootElement;
						if (string.IsNullOrWhiteSpace(capturedTitle))
						{
							capturedTitle = GetString(snapshot, "title");
						}
						if (string.IsNullOrWhiteSpace(capturedThumbnail))
						{
							capturedThumbnail = GetString(snapshot, "thumbnail");
						}
					if (snapshot.TryGetProperty("mediaUrls", out JsonElement mediaUrls) && mediaUrls.ValueKind == JsonValueKind.Array)
						{
							foreach (JsonElement item in mediaUrls.EnumerateArray())
							{
								if (item.ValueKind == JsonValueKind.String && item.GetString() is string media && LooksLikeMedia(media))
								{
									candidates.Add(media);
								}
							}
						}
					}
				}
				catch
				{
				}
				if (candidates.Count > 0 && attempt >= 8)
				{
					break;
				}
			}
			foreach (string mediaUrl in candidates.Take(12))
			{
				cancellationToken.ThrowIfCancellationRequested();
				try
				{
					string value = string.Join("; ", (await web.CoreWebView2.CookieManager.GetCookiesAsync(mediaUrl)).Select((CoreWebView2Cookie c) => c.Name + "=" + c.Value));
					if (!string.IsNullOrWhiteSpace(value))
					{
						cookiesByMediaUrl[mediaUrl] = value;
					}
				}
				catch
				{
				}
			}
		}
		finally
		{
			hiddenWindow.Close();
		}
		cancellationToken.ThrowIfCancellationRequested();
		if (!string.IsNullOrWhiteSpace(capturedTitle) || !string.IsNullOrWhiteSpace(capturedThumbnail))
		{
			SetVideoPreview(string.IsNullOrWhiteSpace(capturedTitle) ? UiText.Get("Vídeo encontrado no player", "Video found in player") : capturedTitle,
				UiText.Get("Informações obtidas pela página do vídeo.", "Information found on the video page."), capturedThumbnail);
		}
		if (candidates.Count == 0)
		{
			throw new InvalidOperationException(UiText.Get("O player não expôs uma URL de mídia acessível. Se o vídeo usar DRM ou stream protegido, não é possível baixá-lo por este método.", "The player did not expose an accessible media URL. DRM or protected streams cannot be downloaded this way."));
		}
		List<VideoFormat> found = new List<VideoFormat>();
		foreach (string mediaUrl in candidates.OrderBy((string u) => (!u.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)) ? 1 : 0))
		{
			cancellationToken.ThrowIfCancellationRequested();
			string mediaCookieHeader = cookiesByMediaUrl.GetValueOrDefault(mediaUrl, "");
			List<string> list = new List<string>
			{
				"--dump-single-json",
				"--no-warnings",
				"--no-playlist",
				"--add-headers",
				"Referer:" + pageUrl
			};
			if (!string.IsNullOrWhiteSpace(mediaCookieHeader))
			{
				list.Add("--add-headers");
				list.Add("Cookie:" + mediaCookieHeader);
			}
			else
			{
				list.AddRange(new[] { "--cookies-from-browser", "chrome" });
			}
			list.Add(mediaUrl);
			(int, string, string) tuple = await RunToolWithCookieFallbackAsync(list, captureOutput: true, cancellationToken: cancellationToken);
			if (tuple.Item1 == 0)
			{
				try
				{
					using JsonDocument jsonDocument2 = JsonDocument.Parse(tuple.Item2);
					JsonElement root = jsonDocument2.RootElement;
					if (root.TryGetProperty("entries", out var value2) && value2.ValueKind == JsonValueKind.Array)
					{
						root = value2.EnumerateArray().FirstOrDefault();
					}
					found.AddRange(ParseFormats(root, mediaUrl, pageUrl, mediaCookieHeader));
				}
				catch
				{
				}
			}
			if (found.Count > 0)
			{
				break;
			}
		}
		availableFormats = found
			.OrderByDescending(f => (long)f.Width * f.Height)
			.ThenByDescending(f => Math.Max(f.Width, f.Height))
			.ThenBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (availableFormats.Count == 0)
		{
			throw new InvalidOperationException(UiText.Get("O player abriu, mas não encontrei um formato de mídia que o downloader consiga ler.", "The player opened, but no media format supported by the downloader was found."));
		}
		qualities.DataSource = availableFormats;
		qualities.DisplayMember = "Label";
		qualities.SelectedIndex = 0;
		qualities.Enabled = true;
		downloadButton.Enabled = true;
		SetVideoPreview(string.IsNullOrWhiteSpace(capturedTitle) ? UiText.Get("Vídeo encontrado no player", "Video found in player") : capturedTitle,
			UiText.Get($"Mídia do player · {availableFormats.Count} opção(ões).", $"Player media · {availableFormats.Count} option(s)."), capturedThumbnail);
		SetStatus(UiText.Get($"Mídia encontrada no player — {availableFormats.Count} opção(ões). Escolha uma qualidade e baixe.", $"Player media found — {availableFormats.Count} option(s). Choose a quality and download."));
	}

	private static bool LooksLikeMedia(string url)
	{
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri? result) && result is not null && (result.Scheme == "http" || result.Scheme == "https"))
		{
			return Regex.IsMatch(result.AbsolutePath, "\\.(mp4|m4v|webm|mov|m3u8|mpd)$", RegexOptions.IgnoreCase);
		}
		return false;
	}

	private static string PortugueseAlbumStatus(int uniqueCount, int totalCount, int duplicatesRemoved)
	{
		string baseMessage = UiText.Get(
			$"Álbum: {uniqueCount} vídeos únicos de {totalCount} entradas",
			$"Album: {uniqueCount} unique videos from {totalCount} entries");
		return duplicatesRemoved > 0
			? baseMessage + UiText.Get($"; {duplicatesRemoved} duplicatas removidas.", $"; {duplicatesRemoved} duplicates removed.")
			: baseMessage + ".";
	}

	private static string GetUniquePlaylistItems(JsonElement entries, out int entryCount, out int uniqueCount)
	{
		entryCount = 0;
		uniqueCount = 0;
		List<int> uniqueIndexes = new List<int>();
		HashSet<string> seenMedia = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (JsonElement entry in entries.EnumerateArray())
		{
			entryCount++;
			string mediaKey = GetPlaylistEntryMediaKey(entry);
			if (string.IsNullOrWhiteSpace(mediaKey))
			{
				mediaKey = "entry:" + GetString(entry, "id");
				if (mediaKey == "entry:") mediaKey += entryCount.ToString(CultureInfo.InvariantCulture);
			}
			if (seenMedia.Add(mediaKey))
			{
				uniqueIndexes.Add(entryCount);
			}
		}
		uniqueCount = uniqueIndexes.Count;
		return uniqueIndexes.Count == 0 ? "" : string.Join(",", uniqueIndexes);
	}

	private static string GetPlaylistEntryMediaKey(JsonElement entry)
	{
		if (entry.ValueKind == JsonValueKind.Object
			&& entry.TryGetProperty("formats", out JsonElement formats)
			&& formats.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement format in formats.EnumerateArray()
				.OrderByDescending(item => (long)Number(item, "width") * Number(item, "height")))
			{
				string mediaUrl = GetString(format, "url");
				if (IsHttpUrl(mediaUrl) && GetString(format, "vcodec") != "none")
				{
					return mediaUrl;
				}
			}
		}
		string directUrl = GetString(entry, "url");
		return IsHttpUrl(directUrl) ? directUrl : "";
	}

	private static List<VideoFormat> ParseFormats(JsonElement root, string sourceUrl, string referer, string cookieHeader = "", string playlistItems = "")
	{
		List<VideoFormat> list = new List<VideoFormat>();
		if (root.ValueKind != JsonValueKind.Object)
		{
			return list;
		}
		if (!root.TryGetProperty("formats", out var value) || value.ValueKind != JsonValueKind.Array)
		{
			VideoFormat? direct = ParseDirectFormat(root, sourceUrl, referer, cookieHeader, playlistItems);
			if (direct != null)
			{
				list.Add(direct);
			}
			return list;
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		bool mediaHasDrm = root.TryGetProperty("has_drm", out JsonElement rootDrm) && rootDrm.ValueKind == JsonValueKind.True;
		bool hasSeparateAudio = value.EnumerateArray().Any(item =>
			GetString(item, "vcodec") == "none"
			&& !string.IsNullOrWhiteSpace(GetString(item, "acodec"))
			&& GetString(item, "acodec") != "none"
			&& IsHttpUrl(GetString(item, "url"))
			&& !(item.TryGetProperty("has_drm", out JsonElement audioDrm) && audioDrm.ValueKind == JsonValueKind.True));
		foreach (JsonElement item in value.EnumerateArray())
		{
			string formatId = GetString(item, "format_id");
			int height = Number(item, "height");
			int width = Number(item, "width");
			string ext = GetString(item, "ext");
			string vcodec = GetString(item, "vcodec");
			string acodec = GetString(item, "acodec");
			string mediaUrl = GetString(item, "url");
			bool hasVideo = !string.IsNullOrWhiteSpace(vcodec) && vcodec != "none";
			bool hasAudio = !string.IsNullOrWhiteSpace(acodec) && acodec != "none";
			bool hasDownloadUrl = Uri.TryCreate(mediaUrl, UriKind.Absolute, out Uri? mediaUri)
				&& (mediaUri.Scheme == "http" || mediaUri.Scheme == "https");
			bool hasDrm = item.TryGetProperty("has_drm", out JsonElement drm) && drm.ValueKind == JsonValueKind.True;
			bool canDownloadWithAudio = hasAudio || (acodec == "none" && hasSeparateAudio);
			if (!string.IsNullOrWhiteSpace(formatId) && hashSet.Add(formatId) && hasVideo && canDownloadWithAudio && hasDownloadUrl && !hasDrm && !mediaHasDrm)
			{
				string formatSelector = hasAudio ? formatId : formatId + "+bestaudio";
				string orientation = width == height ? UiText.Get("quadrado", "square") : width > height ? UiText.Get("horizontal", "landscape") : UiText.Get("vertical", "portrait");
				string label = width > 0 && height > 0
					? $"{width}×{height} ({orientation})"
				: UiText.Get("Dimensões não informadas", "Dimensions unavailable");
				if (!string.IsNullOrWhiteSpace(ext))
				{
					label += " · " + ext;
				}
				label += " · formato " + formatSelector;
				list.Add(new VideoFormat(formatSelector, width, height, label, sourceUrl, referer, cookieHeader, playlistItems));
			}
		}
		if (list.Count == 0)
		{
			VideoFormat? direct = ParseDirectFormat(root, sourceUrl, referer, cookieHeader, playlistItems);
			if (direct != null)
			{
				list.Add(direct);
			}
		}
		return list;
	}

	private static VideoFormat? ParseDirectFormat(JsonElement root, string sourceUrl, string referer, string cookieHeader, string playlistItems = "")
	{
		string formatId = GetString(root, "format_id");
		string ext = GetString(root, "ext");
		string videoExt = GetString(root, "video_ext");
		string vcodec = GetString(root, "vcodec");
		string acodec = GetString(root, "acodec");
		string audioExt = GetString(root, "audio_ext");
		string mediaUrl = GetString(root, "url");
		int width = Number(root, "width");
		int height = Number(root, "height");
		bool hasDrm = (root.TryGetProperty("has_drm", out JsonElement drm) && drm.ValueKind == JsonValueKind.True)
			|| (root.TryGetProperty("_has_drm", out JsonElement internalDrm) && internalDrm.ValueKind == JsonValueKind.True);
		bool hasVideo = (!string.IsNullOrWhiteSpace(vcodec) && vcodec != "none")
			|| (!string.IsNullOrWhiteSpace(videoExt) && videoExt != "none")
			|| (width > 0 && height > 0);
		bool hasDownloadUrl = Uri.TryCreate(mediaUrl, UriKind.Absolute, out Uri? uri)
			&& uri is not null
			&& (uri.Scheme == "http" || uri.Scheme == "https");
		bool audioExplicitlyAbsent = acodec == "none";
		bool audioUnknown = string.IsNullOrWhiteSpace(acodec) && (string.IsNullOrWhiteSpace(audioExt) || audioExt == "none");
		if (string.IsNullOrWhiteSpace(formatId) || string.IsNullOrWhiteSpace(ext)
			|| !hasVideo || !hasDownloadUrl || hasDrm || audioExplicitlyAbsent)
		{
			return null;
		}

		string orientation = width == height ? UiText.Get("quadrado", "square") : width > height ? UiText.Get("horizontal", "landscape") : UiText.Get("vertical", "portrait");
		string label = width > 0 && height > 0 ? $"{width}×{height} ({orientation})" : UiText.Get("Dimensões não informadas", "Dimensions unavailable");
		label += " · " + ext + " · formato " + formatId;
		if (audioUnknown)
		{
			label += UiText.Get(" · áudio não informado pelo site", " · audio not specified by site");
		}
		return new VideoFormat(formatId, width, height, label, sourceUrl, referer, cookieHeader, playlistItems);
	}

	private void UpdateVideoPreview(JsonElement video, int formatCount)
	{
		string title = GetString(video, "title");
		if (string.IsNullOrWhiteSpace(title))
		{
			title = GetString(video, "fulltitle");
		}
		if (string.IsNullOrWhiteSpace(title))
		{
			title = UiText.Get("Vídeo consultado", "Video checked");
		}

		List<string> details = new List<string>();
		string duration = FormatDuration(video);
		if (!string.IsNullOrWhiteSpace(duration)) details.Add(duration);
		int width = Number(video, "width");
		int height = Number(video, "height");
		if (width > 0 && height > 0) details.Add($"{width}×{height}");
		string uploader = GetString(video, "uploader");
		if (!string.IsNullOrWhiteSpace(uploader)) details.Add(uploader);
		details.Add(UiText.Get($"{formatCount} opção(ões) disponíveis", $"{formatCount} option(s) available"));
		SetVideoPreview(title, string.Join(" · ", details), GetThumbnailUrl(video));
	}

	private void SetVideoPreview(string title, string details, string? thumbnailUrl)
	{
		previewTitle.Text = string.IsNullOrWhiteSpace(title) ? UiText.Get("Vídeo consultado", "Video checked") : title;
		previewDetails.Text = details;
		CancelThumbnailLoad();
		ClearThumbnailImage();
		if (IsHttpUrl(thumbnailUrl))
		{
			StartThumbnailLoad(thumbnailUrl!);
		}
		else
		{
			thumbnailPlaceholder.Text = UiText.Get("O site não forneceu uma thumbnail", "The site did not provide a thumbnail");
			thumbnailPlaceholder.Visible = true;
		}
	}

	private void StartThumbnailLoad(string url)
	{
		CancellationTokenSource cts = new CancellationTokenSource();
		thumbnailLoadCts = cts;
		thumbnailPlaceholder.Text = UiText.Get("Carregando thumbnail…", "Loading thumbnail…");
		thumbnailPlaceholder.Visible = true;
		_ = LoadThumbnailAsync(url, cts);
	}

	private async Task LoadThumbnailAsync(string url, CancellationTokenSource cts)
	{
		const int maximumThumbnailBytes = 8 * 1024 * 1024;
		try
		{
			using HttpResponseMessage response = await ThumbnailClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
			response.EnsureSuccessStatusCode();
			string? mediaType = response.Content.Headers.ContentType?.MediaType;
			if (mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true
				|| string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException(UiText.Get("O endereço da thumbnail retornou uma página, não uma imagem.", "The thumbnail URL returned a page, not an image."));
			}
			if (response.Content.Headers.ContentLength > maximumThumbnailBytes)
			{
				throw new InvalidDataException(UiText.Get("A thumbnail excede o limite de tamanho.", "The thumbnail exceeds the size limit."));
			}
			await using Stream input = await response.Content.ReadAsStreamAsync(cts.Token);
			using MemoryStream imageData = new MemoryStream();
			byte[] buffer = new byte[64 * 1024];
			int read;
			while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
			{
				if (imageData.Length + read > maximumThumbnailBytes)
				{
					throw new InvalidDataException(UiText.Get("A thumbnail excede o limite de tamanho.", "The thumbnail exceeds the size limit."));
				}
				await imageData.WriteAsync(buffer.AsMemory(0, read), cts.Token);
			}
			imageData.Position = 0;
			using Image decoded = Image.FromStream(imageData, useEmbeddedColorManagement: false, validateImageData: true);
			if (decoded.Width > 6000 || decoded.Height > 6000 || (long)decoded.Width * decoded.Height > 16_000_000)
			{
				throw new InvalidDataException(UiText.Get("A imagem da thumbnail tem dimensões excessivas.", "The thumbnail image dimensions are too large."));
			}
			Image preview = new Bitmap(decoded);
			if (cts.IsCancellationRequested || IsDisposed)
			{
				preview.Dispose();
				return;
			}
			Image? previous = thumbnailImage.Image;
			thumbnailImage.Image = preview;
			thumbnailImage.Visible = true;
			thumbnailPlaceholder.Visible = false;
			previous?.Dispose();
		}
		catch (OperationCanceledException)
		{
			if (!cts.IsCancellationRequested && !IsDisposed)
			{
				thumbnailPlaceholder.Text = UiText.Get("Thumbnail indisponível neste formato", "Thumbnail unavailable in this format");
				thumbnailPlaceholder.Visible = true;
			}
		}
		catch
		{
			if (!cts.IsCancellationRequested && !IsDisposed)
			{
				thumbnailPlaceholder.Text = UiText.Get("Thumbnail indisponível neste formato", "Thumbnail unavailable in this format");
				thumbnailPlaceholder.Visible = true;
			}
		}
		finally
		{
			if (ReferenceEquals(thumbnailLoadCts, cts))
			{
				thumbnailLoadCts = null;
			}
			cts.Dispose();
		}
	}

	private void CancelThumbnailLoad()
	{
		CancellationTokenSource? current = thumbnailLoadCts;
		thumbnailLoadCts = null;
		current?.Cancel();
	}

	private void ClearThumbnailImage()
	{
		Image? previous = thumbnailImage.Image;
		thumbnailImage.Image = null;
		thumbnailImage.Visible = false;
		previous?.Dispose();
	}

	private static string GetThumbnailUrl(JsonElement video)
	{
		string bestUrl = "";
		long bestArea = -1;
		if (video.TryGetProperty("thumbnails", out JsonElement thumbnails) && thumbnails.ValueKind == JsonValueKind.Array)
		{
			foreach (JsonElement candidate in thumbnails.EnumerateArray())
			{
				string candidateUrl = GetString(candidate, "url");
				if (!IsHttpUrl(candidateUrl)) continue;
				long area = (long)Number(candidate, "width") * Number(candidate, "height");
				if (area > bestArea)
				{
					bestArea = area;
					bestUrl = candidateUrl;
				}
			}
		}
		if (!string.IsNullOrWhiteSpace(bestUrl))
		{
			return bestUrl;
		}
		string thumbnail = GetString(video, "thumbnail");
		return IsHttpUrl(thumbnail) ? thumbnail : "";
	}

	private static string FormatDuration(JsonElement video)
	{
		if (video.ValueKind != JsonValueKind.Object
			|| !video.TryGetProperty("duration", out JsonElement value)
			|| value.ValueKind != JsonValueKind.Number
			|| !value.TryGetDouble(out double seconds)
			|| seconds < 0
			|| seconds > TimeSpan.MaxValue.TotalSeconds)
		{
			return "";
		}
		TimeSpan duration = TimeSpan.FromSeconds(seconds);
		return duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");
	}

	private static bool IsHttpUrl(string? url)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
			&& uri is not null
			&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
	}

	private async Task DownloadAsync()
	{
		if (!TryGetUrl(out string url) || !CheckTools())
		{
			return;
		}
		object? selectedItem = qualities.SelectedItem;
		if (!(selectedItem is VideoFormat selected))
		{
			return;
		}
		string formatPageUrl = string.IsNullOrWhiteSpace(selected.Referer) ? selected.SourceUrl : selected.Referer;
		if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? currentUri)
			|| !Uri.TryCreate(formatPageUrl, UriKind.Absolute, out Uri? formatUri)
			|| currentUri is null
			|| formatUri is null
			|| !currentUri.Equals(formatUri))
		{
			availableFormats = new List<VideoFormat>();
			qualities.DataSource = null;
			qualities.Enabled = false;
			downloadButton.Enabled = false;
			SetStatus(UiText.Get("A URL mudou. Clique em ‘Ver qualidades’ para carregar as opções do vídeo atual.", "The URL changed. Click ‘Check qualities’ to load the current video's options."), error: true);
			return;
		}
		if (!Directory.Exists(folderBox.Text))
		{
			SetStatus(UiText.Get("A pasta escolhida não existe.", "The selected folder does not exist."), error: true);
			return;
		}
		using CancellationTokenSource operationCts = new CancellationTokenSource();
		activeOperationCts = operationCts;
		SetBusy(value: true);
		SetProgress(0);
		SetStatus(UiText.Get("Preparando o download…", "Preparing download…"));
		try
		{
			string text2 = Path.Combine(folderBox.Text, "%(title).180B [%(id)s].%(ext)s");
			List<string> list = new List<string> { "--newline", "--progress" };
			if (string.IsNullOrWhiteSpace(selected.PlaylistItems))
			{
				list.Add("--no-playlist");
			}
			else
			{
				list.AddRange(new[] { "--playlist-items", selected.PlaylistItems });
			}
			if (string.IsNullOrWhiteSpace(selected.CookieHeader))
			{
				list.AddRange(new[] { "--cookies-from-browser", "chrome" });
			}
			if (!string.IsNullOrWhiteSpace(selected.Referer))
			{
				list.Add("--add-headers");
				list.Add("Referer:" + selected.Referer);
			}
			if (!string.IsNullOrWhiteSpace(selected.CookieHeader))
			{
				list.Add("--add-headers");
				list.Add("Cookie:" + selected.CookieHeader);
			}
			list.AddRange(new[] { "--ffmpeg-location", Path.Combine(AppContext.BaseDirectory, "tools"), "--format", selected.Id, "--output", text2, selected.SourceUrl });
			(int, string, string) tuple = await RunToolWithCookieFallbackAsync(list, captureOutput: false, UpdateProgress, operationCts.Token);
			if (tuple.Item1 != 0)
			{
				throw new InvalidOperationException(ShortError(tuple.Item3));
			}
			SetProgress(100);
			SetStatus(UiText.Get("Download concluído. O arquivo está na pasta escolhida.", "Download complete. The file is in the selected folder."));
		}
		catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
		{
			SetStatus(UiText.Get("Download cancelado. Você pode tentar novamente.", "Download canceled. You can try again."));
		}
		catch (Exception ex)
		{
			if (string.IsNullOrWhiteSpace(selected.Referer))
			{
				try
				{
					await CaptureMediaInBackgroundAsync(url, operationCts.Token);
				}
				catch (OperationCanceledException) when (operationCts.IsCancellationRequested)
				{
					SetStatus(UiText.Get("Download cancelado. Você pode tentar novamente.", "Download canceled. You can try again."));
				}
				catch (Exception ex2)
				{
					SetStatus(ex2.Message, error: true);
				}
			}
			else
			{
				SetStatus(ex.Message, error: true);
			}
		}
		finally
		{
			if (ReferenceEquals(activeOperationCts, operationCts))
			{
				activeOperationCts = null;
			}
			SetBusy(value: false);
		}
	}

	private async Task<(int ExitCode, string Output, string Error)> RunToolAsync(IEnumerable<string> args, bool captureOutput, Action<string>? onLine = null, CancellationToken cancellationToken = default)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo(YtDlp)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			WorkingDirectory = AppContext.BaseDirectory
		};
		foreach (string arg in args)
		{
			processStartInfo.ArgumentList.Add(arg);
		}
		using Process process = new Process
		{
			StartInfo = processStartInfo
		};
		StringBuilder output = new StringBuilder();
		StringBuilder errors = new StringBuilder();
		TaskCompletionSource<bool> outputClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		TaskCompletionSource<bool> errorClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (e.Data == null)
			{
				outputClosed.TrySetResult(true);
			}
			else
			{
				if (captureOutput)
				{
					output.AppendLine(e.Data);
				}
				onLine?.Invoke(e.Data);
			}
		};
		process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (e.Data == null)
			{
				errorClosed.TrySetResult(true);
			}
			else
			{
				errors.AppendLine(e.Data);
				onLine?.Invoke(e.Data);
			}
		};
		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		using CancellationTokenRegistration cancellation = cancellationToken.Register(() =>
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill(entireProcessTree: true);
				}
			}
			catch
			{
			}
		});
		try
		{
			await process.WaitForExitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			try
			{
				if (!process.HasExited)
				{
					process.Kill(entireProcessTree: true);
				}
			}
			catch
			{
			}
			await process.WaitForExitAsync();
			await Task.WhenAll(outputClosed.Task, errorClosed.Task);
			throw;
		}
		await Task.WhenAll(outputClosed.Task, errorClosed.Task);
		return (ExitCode: process.ExitCode, Output: output.ToString(), Error: errors.ToString());
	}

	private async Task<(int ExitCode, string Output, string Error)> RunToolWithCookieFallbackAsync(List<string> args, bool captureOutput, Action<string>? onLine = null, CancellationToken cancellationToken = default)
	{
		(int, string, string) result = await RunToolAsync(args, captureOutput, onLine, cancellationToken);
		if (result.Item1 == 0 || !result.Item3.Contains("cookie database", StringComparison.OrdinalIgnoreCase))
		{
			return result;
		}
		List<string> list = new List<string>(args);
		int num = list.FindIndex((string x) => x.Equals("--cookies-from-browser", StringComparison.OrdinalIgnoreCase));
		if (num >= 0)
		{
			list.RemoveRange(num, Math.Min(2, list.Count - num));
		}
		return await RunToolAsync(list, captureOutput, onLine, cancellationToken);
	}

	private void UpdateProgress(string line)
	{
		if (IsDisposed || !IsHandleCreated)
		{
			return;
		}
		Match match = Regex.Match(line, "(\\d{1,3}(?:\\.\\d+)?)%");
		if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
		{
			try
			{
				BeginInvoke(delegate
				{
					SetProgress((int)pct);
					SetStatus(UiText.Get($"Baixando… {(int)pct}%", $"Downloading… {(int)pct}%"));
				});
			}
			catch (InvalidOperationException)
			{
			}
		}
	}

	private void SetProgress(int value)
	{
		if (IsDisposed)
		{
			return;
		}
		progressValue = Math.Clamp(value, 0, 100);
		progressFill.Width = (int)((double)(progressTrack.ClientSize.Width * progressValue) / 100.0);
	}

	private void SetBusy(bool value)
	{
		if (IsDisposed)
		{
			return;
		}
		inspectButton.Enabled = !value;
		downloadButton.Enabled = !value && availableFormats.Count > 0;
		cancelButton.Enabled = value;
		folderButton.Enabled = !value;
		urlBox.Enabled = !value;
		qualities.Enabled = !value && availableFormats.Count > 0;
		Cursor = (value ? Cursors.WaitCursor : Cursors.Default);
	}

	private bool TryGetUrl(out string url)
	{
		url = urlBox.Text.Trim();
		if (Uri.TryCreate(url, UriKind.Absolute, out Uri? result) && result is not null && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
		{
			return true;
		}
		SetStatus(UiText.Get("Digite uma URL válida começando com http:// ou https://.", "Enter a valid URL starting with http:// or https://."), error: true);
		return false;
	}

	private bool CheckTools()
	{
		if (File.Exists(YtDlp))
		{
			return true;
		}
		SetStatus(UiText.Get("yt-dlp não encontrado. Mantenha tools\\yt-dlp.exe ao lado deste executável.", "yt-dlp was not found. Keep tools\\yt-dlp.exe beside this app."), error: true);
		return false;
	}

	private void SetStatus(string message, bool error = false)
	{
		if (IsDisposed)
		{
			return;
		}
		status.Text = message;
		status.ForeColor = (error ? Color.Firebrick : Color.DimGray);
	}

	private static int Number(JsonElement e, string key)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number)
		{
			return 0;
		}
		if (!value.TryGetDouble(out double number) || !double.IsFinite(number) || number < 0 || number > int.MaxValue || number != Math.Truncate(number))
		{
			return 0;
		}
		return (int)number;
	}

	private static string GetString(JsonElement e, string key)
	{
		if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
		{
			return "";
		}
		return value.GetString() ?? "";
	}

	private static string ShortError(string text)
	{
		if (IsPiracyBlocked(text))
		{
			return UiText.Get("O yt-dlp não oferece suporte a este site porque o classificou como usado principalmente para pirataria. O app não tentará extrair a mídia por outro método. Procure uma fonte oficial ou autorizada para o vídeo.", "yt-dlp does not support this site because it classifies it as primarily used for piracy. The app will not try another extraction method. Look for an official or authorized source for the video.");
		}
		if (text.Contains("requested format is not available", StringComparison.OrdinalIgnoreCase)
			|| text.Contains("requested format not available", StringComparison.OrdinalIgnoreCase))
		{
			return UiText.Get("A qualidade selecionada deixou de estar disponível. Clique em ‘Ver qualidades’ para consultar as opções atuais e tente novamente.", "The selected quality is no longer available. Click ‘Check qualities’ to reload the current options and try again.");
		}
		if (text.Contains("[Beeg]", StringComparison.OrdinalIgnoreCase) && text.Contains("HTTP Error 400", StringComparison.OrdinalIgnoreCase))
		{
			return UiText.Get("O Beeg retornou HTTP 400; o extrator atual não consegue consultar este site. Tente novamente após uma correção do yt-dlp.", "Beeg returned HTTP 400; the current extractor cannot check this site. Try again after yt-dlp is updated.");
		}
		string[] source = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		return source.LastOrDefault((string x) => x.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase)) ?? source.LastOrDefault() ?? UiText.Get("Não foi possível acessar este vídeo.", "Could not access this video.");
	}

	private static bool IsPiracyBlocked(string text)
	{
		return text.Contains("[Piracy]", StringComparison.OrdinalIgnoreCase)
			|| text.Contains("primarily used for piracy", StringComparison.OrdinalIgnoreCase);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			activeOperationCts?.Cancel();
			CancelThumbnailLoad();
			ClearThumbnailImage();
		}
		base.Dispose(disposing);
	}
}
