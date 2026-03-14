using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Interfaces;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ThreatIntelViewModel : ViewModelBase
{
    private readonly IKnowledgeBase? _knowledgeBase;

    public ThreatReputationViewModel ReputationVm { get; }
    public IncidentReportViewModel IncidentsVm { get; }
    public YaraRuleViewModel YaraVm { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    // Summary statistics
    [ObservableProperty]
    private int _totalSignatures;

    [ObservableProperty]
    private int _totalLookups;

    [ObservableProperty]
    private int _threatsIdentified;

    [ObservableProperty]
    private string _lastUpdated = "Never";

    [ObservableProperty]
    private string _feedStatus = "Checking...";

    [ObservableProperty]
    private bool _isFeedOnline;

    // Quick hash lookup
    [ObservableProperty]
    private string _hashInput = string.Empty;

    [ObservableProperty]
    private string _hashResult = string.Empty;

    [ObservableProperty]
    private bool _isLookingUp;

    [ObservableProperty]
    private bool _hasLookupResult;

    // Signature list
    public ObservableCollection<SignatureEntry> RecentSignatures { get; } = new();

    public ThreatIntelViewModel(
        ThreatReputationViewModel reputationVm,
        IncidentReportViewModel incidentsVm,
        YaraRuleViewModel yaraVm,
        IKnowledgeBase? knowledgeBase = null)
    {
        Title = "Threat Intel";
        ReputationVm = reputationVm;
        IncidentsVm = incidentsVm;
        YaraVm = yaraVm;
        _knowledgeBase = knowledgeBase;

        _ = LoadSummaryAsync();
    }

    private async Task LoadSummaryAsync()
    {
        try
        {
            if (_knowledgeBase == null)
            {
                FeedStatus = "Knowledge base not available";
                IsFeedOnline = false;
                return;
            }

            TotalSignatures = await _knowledgeBase.GetSignatureCountAsync();
            LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            FeedStatus = "Online";
            IsFeedOnline = true;

            // Load recent signatures for display
            var sigs = await _knowledgeBase.GetAllSignaturesAsync();
            RecentSignatures.Clear();
            foreach (var sig in sigs.Take(50))
            {
                RecentSignatures.Add(new SignatureEntry
                {
                    Name = sig.Name,
                    Hash = sig.Sha256Hash,
                    Severity = sig.Severity.ToString(),
                    Category = sig.Family,
                    AddedDate = sig.FirstSeen.ToString("yyyy-MM-dd")
                });
            }
        }
        catch
        {
            FeedStatus = "Error loading data";
            IsFeedOnline = false;
        }
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        await LoadSummaryAsync();

        // Sync counters from reputation VM
        TotalLookups = ReputationVm.TotalLookups;
        ThreatsIdentified = ReputationVm.ThreatsFound;
    }

    [RelayCommand]
    private async Task QuickHashLookupAsync()
    {
        if (string.IsNullOrWhiteSpace(HashInput) || _knowledgeBase == null)
            return;

        IsLookingUp = true;
        HasLookupResult = false;
        HashResult = string.Empty;

        try
        {
            var hash = HashInput.Trim();
            var result = await _knowledgeBase.LookupHashAsync(hash);

            if (result != null)
            {
                HashResult = $"THREAT FOUND: {result.Name}\n" +
                             $"Type: {result.Family}\n" +
                             $"Severity: {result.Severity}\n" +
                             $"Description: {result.Description}";
                ThreatsIdentified++;
            }
            else
            {
                HashResult = "No threats found for this hash. File appears clean.";
            }

            TotalLookups++;
            HasLookupResult = true;
        }
        catch (Exception ex)
        {
            HashResult = $"Lookup error: {ex.Message}";
            HasLookupResult = true;
        }
        finally
        {
            IsLookingUp = false;
        }
    }
}

public class SignatureEntry
{
    public string Name { get; set; } = "";
    public string Hash { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Category { get; set; } = "";
    public string AddedDate { get; set; } = "";
}
