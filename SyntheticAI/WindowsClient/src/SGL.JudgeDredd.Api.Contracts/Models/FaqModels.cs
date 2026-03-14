namespace SGL.JudgeDredd.Api.Contracts.Models;

public class FaqEntry
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string AskedBy { get; set; } = string.Empty;
    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnsweredAt { get; set; }
    public bool IsAnswered { get; set; }
}

public class FaqAskRequest
{
    public string Question { get; set; } = string.Empty;
}

public class FaqAnswerRequest
{
    public string Answer { get; set; } = string.Empty;
}
