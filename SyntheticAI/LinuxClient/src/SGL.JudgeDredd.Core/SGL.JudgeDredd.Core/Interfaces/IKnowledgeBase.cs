namespace SGL.JudgeDredd.Core.Interfaces;

using SGL.JudgeDredd.Core.Models;

public interface IKnowledgeBase
{
    Task<ThreatSignature?> LookupHashAsync(string sha256Hash);
    Task AddSignatureAsync(ThreatSignature signature);
    Task<IReadOnlyList<RemediationAction>> GetSolutionsForThreatAsync(int threatId);
    Task<IReadOnlyList<FirewallPreset>> GetFirewallTemplatesAsync(string category);
    Task LogScanResultAsync(ScanSession session);
    Task QuarantineFileAsync(string filePath, ThreatInfo threat);
    Task SaveThreatAnalysisAsync(string systemData, string analysisResult, string summary, int severityScore);
    Task<int> GetSignatureCountAsync();
    Task<IReadOnlyList<ThreatSignature>> GetAllSignaturesAsync();
}
