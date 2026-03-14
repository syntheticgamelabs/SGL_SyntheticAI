using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ShareViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;

    [ObservableProperty]
    private ImageSource? _qrCodeImage;

    [ObservableProperty]
    private string _downloadUrl = string.Empty;

    [ObservableProperty]
    private string _downloadSizeText = "Calculating...";

    [ObservableProperty]
    private string _statusMessage = "Click 'Generate QR Code' to create a shareable download link.";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _hasQrCode;

    [ObservableProperty]
    private string _selectedShareType = "Mobile APK";

    public string[] ShareTypes { get; } = { "Mobile APK", "Desktop Client", "Linux Client", "Server Info" };

    public ShareViewModel(AppSettings appSettings)
    {
        _appSettings = appSettings;
        Title = "Share";

        // Build the download URL from the configured domain
        var domain = appSettings.Server.PublicDomain;
        if (string.IsNullOrEmpty(domain))
            domain = "syntheticgamelabs.dpdns.org";

        UpdateDownloadUrl(domain);
    }

    partial void OnSelectedShareTypeChanged(string value)
    {
        var domain = _appSettings.Server.PublicDomain;
        if (string.IsNullOrEmpty(domain))
            domain = "syntheticgamelabs.dpdns.org";

        UpdateDownloadUrl(domain);

        // Reset QR code when share type changes
        HasQrCode = false;
        QrCodeImage = null;
        StatusMessage = $"Share type changed to '{value}'. Click 'Generate QR Code' to create a new code.";
    }

    private void UpdateDownloadUrl(string domain)
    {
        DownloadUrl = SelectedShareType switch
        {
            "Mobile APK" => $"https://{domain}/api/v1/mobile/download",
            "Desktop Client" => $"https://{domain}/api/v1/client/download",
            "Linux Client" => $"https://{domain}/api/v1/linux-client/download",
            "Server Info" => $"https://{domain}",
            _ => $"https://{domain}"
        };
    }

    [RelayCommand]
    private async Task GenerateQrCodeAsync()
    {
        IsGenerating = true;
        StatusMessage = "Generating QR code...";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);

        try
        {
            // First, check APK size from local data/mobile folder or server
            await DetectApkSizeAsync();

            // Generate QR code using QRCoder
            var qrBitmap = await Task.Run(() =>
            {
                using var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(DownloadUrl, QRCodeGenerator.ECCLevel.H);
                using var qrCode = new QRCoder.BitmapByteQRCode(qrCodeData);
                var qrBytes = qrCode.GetGraphic(20);

                // Load as System.Drawing.Bitmap
                using var ms = new MemoryStream(qrBytes);
                var baseBitmap = new Bitmap(ms);

                // Create final composite image with branding
                var finalWidth = baseBitmap.Width;
                var finalHeight = baseBitmap.Height + 80; // extra space at bottom for size info
                var composite = new Bitmap(finalWidth, finalHeight);

                using var gfx = Graphics.FromImage(composite);
                gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // White background
                gfx.Clear(System.Drawing.Color.White);

                // Draw QR code
                gfx.DrawImage(baseBitmap, 0, 0);

                // Draw JD logo in center of QR code
                var centerX = baseBitmap.Width / 2;
                var centerY = baseBitmap.Height / 2;
                var logoSize = baseBitmap.Width / 5;

                // White circle background for logo
                using var whiteBrush = new SolidBrush(System.Drawing.Color.White);
                gfx.FillEllipse(whiteBrush, centerX - logoSize / 2 - 4, centerY - logoSize / 2 - 4, logoSize + 8, logoSize + 8);

                // Dark circle for logo
                using var darkBrush = new SolidBrush(System.Drawing.Color.FromArgb(18, 18, 30));
                gfx.FillEllipse(darkBrush, centerX - logoSize / 2, centerY - logoSize / 2, logoSize, logoSize);

                // SA text
                using var jdFont = new Font("Segoe UI", logoSize * 0.3f, FontStyle.Bold);
                using var jdBrush = new SolidBrush(System.Drawing.Color.FromArgb(0, 200, 255));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                gfx.DrawString("SA", jdFont, jdBrush, centerX, centerY, sf);

                // Bottom info bar
                var infoY = baseBitmap.Height + 5;
                using var infoFont = new Font("Segoe UI", 14, FontStyle.Regular);
                using var boldFont = new Font("Segoe UI", 14, FontStyle.Bold);
                using var blackBrush = new SolidBrush(System.Drawing.Color.Black);

                gfx.DrawString($"SGL SyntheticAI - {SelectedShareType}", boldFont, blackBrush, 10, infoY);
                gfx.DrawString($"Download: {DownloadSizeText}", infoFont, blackBrush, 10, infoY + 25);
                gfx.DrawString("Scan to install", infoFont,
                    new SolidBrush(System.Drawing.Color.FromArgb(100, 100, 100)), 10, infoY + 50);

                return composite;
            });

            // Convert System.Drawing.Bitmap to WPF ImageSource
            using var memStream = new MemoryStream();
            qrBitmap.Save(memStream, ImageFormat.Png);
            memStream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            QrCodeImage = bitmapImage;
            HasQrCode = true;
            StatusMessage = $"QR code ready! Download size: {DownloadSizeText}";

            // Also save to disk
            var saveDir = Path.Combine(AppContext.BaseDirectory, "data", "share");
            Directory.CreateDirectory(saveDir);
            var safeShareType = SelectedShareType.Replace(" ", "_").ToLowerInvariant();
            var savePath = Path.Combine(saveDir, $"syntheticai_{safeShareType}_qr.png");
            qrBitmap.Save(savePath, ImageFormat.Png);

            qrBitmap.Dispose();

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble($"QR code generated! Friends can scan to download SyntheticAI ({SelectedShareType}).");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to generate QR code: {ex.Message}";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"QR generation failed: {ex.Message}");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task SaveQrCodeAsync()
    {
        if (QrCodeImage == null)
        {
            StatusMessage = "Generate a QR code first.";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save QR Code Image",
            Filter = "PNG Image|*.png",
            FileName = $"SyntheticAI_{SelectedShareType.Replace(" ", "_")}_QR.png",
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var safeShareType = SelectedShareType.Replace(" ", "_").ToLowerInvariant();
                var srcPath = Path.Combine(AppContext.BaseDirectory, "data", "share", $"syntheticai_{safeShareType}_qr.png");
                if (File.Exists(srcPath))
                {
                    File.Copy(srcPath, dialog.FileName, overwrite: true);
                    StatusMessage = $"QR code saved to {dialog.FileName}";
                    await AvatarViewModel.Instance.ShowSpeechBubble("QR code saved!");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void CopyDownloadLink()
    {
        try
        {
            System.Windows.Clipboard.SetText(DownloadUrl);
            StatusMessage = "Download link copied to clipboard!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to copy: {ex.Message}";
        }
    }

    private async Task DetectApkSizeAsync()
    {
        // Try local APK first (server stores it in data/mobile/)
        var localApkDir = Path.Combine(AppContext.BaseDirectory, "data", "mobile");
        if (Directory.Exists(localApkDir))
        {
            var apkFiles = Directory.GetFiles(localApkDir, "*.apk");
            if (apkFiles.Length > 0)
            {
                var fi = new FileInfo(apkFiles[0]);
                DownloadSizeText = FormatFileSize(fi.Length);
                return;
            }
        }

        // Fallback: try querying server
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var response = await httpClient.GetAsync($"{DownloadUrl}/info");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                DownloadSizeText = content;
                return;
            }
        }
        catch { /* server not reachable */ }

        DownloadSizeText = "~1.8 GB (includes AI model)";
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
