using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Mobile.Models;
using SGL.JudgeDredd.Mobile.Services;
using System.Collections.ObjectModel;

namespace SGL.JudgeDredd.Mobile.ViewModels;

public partial class GitViewModel : ObservableObject
{
    private readonly GitService _gitService;
    private readonly ApiClient _apiClient;

    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private double _downloadProgress;
    [ObservableProperty] private string _statusText = "Search GitHub repositories";
    [ObservableProperty] private string _selectedTab = "Search";
    [ObservableProperty] private string _fileContent = string.Empty;
    [ObservableProperty] private string _currentPath = string.Empty;
    private string _currentOwner = string.Empty;
    private string _currentRepo = string.Empty;

    // Broadcast Chat removed — use Broadcast tab instead

    public ObservableCollection<GitRepoInfo> SearchResults { get; } = new();
    public ObservableCollection<GitRepoInfo> LocalRepos { get; } = new();
    public ObservableCollection<GitFileItem> CurrentFiles { get; } = new();

    public GitViewModel(GitService gitService, ApiClient apiClient)
    {
        _gitService = gitService;
        _apiClient = apiClient;
        _gitService.StatusChanged += (_, s) =>
            MainThread.BeginInvokeOnMainThread(() => StatusText = s);
        _gitService.DownloadProgress += (_, p) =>
            MainThread.BeginInvokeOnMainThread(() => DownloadProgress = p * 100);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        IsSearching = true;
        SearchResults.Clear();

        var results = await _gitService.SearchReposAsync(SearchQuery);
        foreach (var repo in results)
            SearchResults.Add(repo);

        StatusText = $"Found {results.Count} repositories";
        IsSearching = false;
    }

    [RelayCommand]
    private async Task BrowseRepoAsync(GitRepoInfo? repo)
    {
        if (repo == null) return;
        _currentOwner = repo.Owner;
        _currentRepo = repo.Name;
        CurrentPath = "";
        CurrentFiles.Clear();

        var files = await _gitService.GetRepoContentsAsync(repo.Owner, repo.Name);
        foreach (var f in files)
            CurrentFiles.Add(f);

        SelectedTab = "Browse";
        StatusText = $"Browsing {repo.FullName}";
    }

    [RelayCommand]
    private async Task OpenFileAsync(GitFileItem? item)
    {
        if (item == null) return;

        if (item.IsDirectory)
        {
            CurrentFiles.Clear();
            CurrentPath = item.Path;
            var files = await _gitService.GetRepoContentsAsync(_currentOwner, _currentRepo, item.Path);
            foreach (var f in files)
                CurrentFiles.Add(f);
        }
        else
        {
            FileContent = await _gitService.GetFileContentAsync(_currentOwner, _currentRepo, item.Path);
            StatusText = $"Viewing {item.Name}";
        }
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return;
        var parent = Path.GetDirectoryName(CurrentPath)?.Replace('\\', '/') ?? "";
        CurrentPath = parent;
        CurrentFiles.Clear();
        var files = await _gitService.GetRepoContentsAsync(_currentOwner, _currentRepo, parent);
        foreach (var f in files)
            CurrentFiles.Add(f);
    }

    [RelayCommand]
    private async Task DownloadRepoAsync(GitRepoInfo? repo)
    {
        if (repo == null) return;
        IsDownloading = true;
        DownloadProgress = 0;

        var progress = new Progress<double>(p => DownloadProgress = p * 100);
        await _gitService.DownloadRepoAsync(repo, progress);

        IsDownloading = false;
        await LoadLocalReposAsync();
    }

    [RelayCommand]
    private async Task LoadLocalReposAsync()
    {
        LocalRepos.Clear();
        var repos = _gitService.GetLocalRepos();
        foreach (var r in repos)
            LocalRepos.Add(r);
        SelectedTab = "Local";
    }

    [RelayCommand]
    private async Task DeleteLocalRepoAsync(GitRepoInfo? repo)
    {
        if (repo == null) return;
        var confirm = await Shell.Current.DisplayAlert("Delete Repository",
            $"Delete local copy of {repo.Name}?", "Delete", "Cancel");
        if (confirm)
        {
            await _gitService.DeleteLocalRepoAsync(repo.Name);
            await LoadLocalReposAsync();
        }
    }

    [RelayCommand]
    private void SwitchTab(string tab)
    {
        SelectedTab = tab;
    }
}
