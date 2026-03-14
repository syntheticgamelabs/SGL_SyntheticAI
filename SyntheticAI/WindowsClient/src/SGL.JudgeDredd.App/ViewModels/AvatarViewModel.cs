using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class AvatarViewModel : ObservableObject
{
    private static readonly Lazy<AvatarViewModel> _instance = new(() => new AvatarViewModel());
    public static AvatarViewModel Instance => _instance.Value;

    private CancellationTokenSource? _speechBubbleCts;

    [ObservableProperty]
    private AvatarExpression _currentExpression = AvatarExpression.Idle;

    [ObservableProperty]
    private string _speechBubbleText = string.Empty;

    [ObservableProperty]
    private bool _isSpeechBubbleVisible;

    private AvatarViewModel() { }

    public void SetExpression(AvatarExpression expression)
    {
        CurrentExpression = expression;
    }

    public async Task ShowSpeechBubble(string text, int durationMs = 4000)
    {
        _speechBubbleCts?.Cancel();
        _speechBubbleCts = new CancellationTokenSource();
        var token = _speechBubbleCts.Token;

        SpeechBubbleText = text;
        IsSpeechBubbleVisible = true;

        try
        {
            await Task.Delay(durationMs, token);
            IsSpeechBubbleVisible = false;
            SpeechBubbleText = string.Empty;
        }
        catch (TaskCanceledException)
        {
            // New speech bubble replaced this one; do nothing
        }
    }
}
