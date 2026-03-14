using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using SGL.JudgeDredd.Mobile.Services;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class QrShareViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;

    [ObservableProperty] private string _shareUrl = string.Empty;
    [ObservableProperty] private string _qrContent = string.Empty;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string _statusText = "Generate a QR code to share SyntheticAI";
    [ObservableProperty] private string _selectedShareType = "Mobile APK";
    [ObservableProperty] private bool _hasQrCode;
    [ObservableProperty] private ImageSource? _qrImageSource;

    public string[] ShareTypes { get; } = { "Mobile APK", "Desktop Client", "Linux Client", "Server Info" };

    public QrShareViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    private async Task GenerateQrAsync()
    {
        IsGenerating = true;
        HasQrCode = false;
        QrImageSource = null;

        try
        {
            var serverUrl = _apiClient.BaseUrl.TrimEnd('/');

            ShareUrl = SelectedShareType switch
            {
                "Mobile APK" => $"{serverUrl}/api/v1/mobile/download",
                "Desktop Client" => $"{serverUrl}/api/v1/client/download",
                "Linux Client" => $"{serverUrl}/api/v1/linux-client/download",
                "Server Info" => serverUrl,
                _ => serverUrl
            };

            // Verify the link is reachable
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync(ShareUrl, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    StatusText = $"Link verified - {SelectedShareType} available for download";
                }
                else
                {
                    StatusText = $"Warning: Server returned {(int)response.StatusCode}. QR code generated anyway.";
                }
            }
            catch
            {
                StatusText = "Could not verify link. QR code generated with server URL.";
            }

            // Generate QR code bitmap using QRCoder
            var pngBytes = await Task.Run(() =>
            {
                using var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(ShareUrl, QRCodeGenerator.ECCLevel.M);
                var qrCode = new PngByteQRCode(qrCodeData);
                return qrCode.GetGraphic(10);
            });

            // Convert PNG bytes to MAUI ImageSource
            QrImageSource = ImageSource.FromStream(() => new MemoryStream(pngBytes));
            QrContent = ShareUrl;
            HasQrCode = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }

        IsGenerating = false;
    }

    [RelayCommand]
    private async Task ShareLinkAsync()
    {
        if (string.IsNullOrEmpty(ShareUrl)) return;

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Share SGL SyntheticAI",
            Text = $"Download SGL SyntheticAI Security Suite: {ShareUrl}",
            Uri = ShareUrl
        });
    }

    [RelayCommand]
    private async Task CopyLinkAsync()
    {
        if (string.IsNullOrEmpty(ShareUrl)) return;
        await Clipboard.Default.SetTextAsync(ShareUrl);
        StatusText = "Link copied to clipboard!";
    }
}
