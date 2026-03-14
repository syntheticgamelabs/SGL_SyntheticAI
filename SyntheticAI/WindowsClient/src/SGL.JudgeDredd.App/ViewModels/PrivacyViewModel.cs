using CommunityToolkit.Mvvm.ComponentModel;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class PrivacyViewModel : ViewModelBase
{
    public TrackerViewModel TrackersVm { get; }
    public WebcamMicViewModel WebcamMicVm { get; }
    public AiActivityViewModel AiActivityVm { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public PrivacyViewModel(
        TrackerViewModel trackersVm,
        WebcamMicViewModel webcamMicVm,
        AiActivityViewModel aiActivityVm)
    {
        Title = "Privacy";
        TrackersVm = trackersVm;
        WebcamMicVm = webcamMicVm;
        AiActivityVm = aiActivityVm;
    }
}
