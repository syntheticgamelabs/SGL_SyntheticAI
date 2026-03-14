using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Shared.Configuration;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class FaqViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;

    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ask a question or browse existing FAQ entries";

    public ObservableCollection<FaqDisplayItem> FaqItems { get; } = new();

    public FaqViewModel(AppSettings appSettings)
    {
        _appSettings = appSettings;
        Title = "FAQ";
    }

    [RelayCommand]
    private async Task LoadFaqAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading FAQ...";

        try
        {
            var domain = _appSettings.Server.PublicDomain;
            if (string.IsNullOrEmpty(domain)) domain = "syntheticgamelabs.dpdns.org";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync($"https://{domain}/api/v1/faq");

            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<List<FaqDisplayItem>>();
                FaqItems.Clear();
                if (items != null)
                {
                    foreach (var item in items)
                        FaqItems.Add(item);
                }
                StatusMessage = $"Loaded {FaqItems.Count} FAQ item(s)";
            }
            else
            {
                StatusMessage = $"Server returned {(int)response.StatusCode}. Check server connection.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading FAQ: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AskQuestionAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionText))
        {
            StatusMessage = "Please enter a question first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Submitting question...";

        try
        {
            var domain = _appSettings.Server.PublicDomain;
            if (string.IsNullOrEmpty(domain)) domain = "syntheticgamelabs.dpdns.org";

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.PostAsJsonAsync(
                $"https://{domain}/api/v1/faq/ask",
                new { question = QuestionText });

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Question submitted!";
                QuestionText = string.Empty;
                await LoadFaqAsync();
            }
            else
            {
                StatusMessage = "Failed to submit question.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class FaqDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string AskedBy { get; set; } = string.Empty;
    public DateTime AskedAt { get; set; }
    public bool IsAnswered { get; set; }
    public string AnswerDisplay => IsAnswered ? Answer : "Awaiting answer...";
    public string MetadataDisplay => $"Asked by {AskedBy} on {AskedAt:MMM dd, yyyy}";
}
