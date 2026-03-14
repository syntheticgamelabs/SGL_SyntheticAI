namespace SGL.JudgeDredd.Core.Events;

using SGL.JudgeDredd.Core.Models;

public class SecurityAlertEvent : EventArgs
{
    public SecurityAlert Alert { get; init; } = null!;
}
