using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Services;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class FaqViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;

    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "Ask a question or browse existing FAQ";

    public ObservableCollection<FaqItem> FaqItems { get; } = new();

    public FaqViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    private async Task LoadFaqAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading FAQ...";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync($"{_apiClient.BaseUrl.TrimEnd('/')}/api/v1/faq");

            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<List<FaqItem>>();
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
                StatusMessage = "Could not load FAQ from server.";
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

    [RelayCommand]
    private async Task AskQuestionAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionText))
        {
            StatusMessage = "Please enter a question.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Submitting question...";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.PostAsJsonAsync(
                $"{_apiClient.BaseUrl.TrimEnd('/')}/api/v1/faq/ask",
                new { question = QuestionText });

            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "Question submitted successfully!";
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

    public void OnAppearing()
    {
        _ = LoadFaqAsync();
    }
}

public class FaqItem
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string AskedBy { get; set; } = string.Empty;
    public DateTime AskedAt { get; set; }
    public bool IsAnswered { get; set; }
}
