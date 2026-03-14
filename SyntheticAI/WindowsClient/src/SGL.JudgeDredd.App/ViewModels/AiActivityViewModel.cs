using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class AiActivityViewModel : ViewModelBase
{
    private DispatcherTimer? _monitorTimer;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _monitorStatus = "Idle - Click Start to begin AI activity monitoring";

    [ObservableProperty]
    private int _aiProcessesDetected;

    public ObservableCollection<AiActivityEntry> Activities { get; } = [];
    public ObservableCollection<AiActivityEntry> ActivityLog { get; } = [];

    // Known AI/LLM process names and descriptions
    private static readonly Dictionary<string, string> KnownAiProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Local LLMs
        { "ollama", "Ollama - Local LLM Server" },
        { "ollama_llama_server", "Ollama LLaMA Backend" },
        { "koboldcpp", "KoboldCpp - Local LLM" },
        { "llamacpp", "llama.cpp - Local LLM" },
        { "llama-server", "llama.cpp Server" },
        { "server", "Possible LLM Server" },
        { "lmstudio", "LM Studio - Local LLM" },
        { "jan", "Jan - Local AI Assistant" },
        { "gpt4all", "GPT4All - Local LLM" },
        { "text-generation-webui", "Text Generation WebUI" },
        { "oobabooga", "Oobabooga Text Gen" },
        { "privateGPT", "PrivateGPT - Local Docs AI" },
        { "h2ogpt", "H2O GPT - Local LLM" },
        { "localai", "LocalAI Server" },
        { "vllm", "vLLM - Fast LLM Inference" },

        // AI-adjacent tools
        { "stable-diffusion-webui", "Stable Diffusion (Image Gen)" },
        { "ComfyUI", "ComfyUI (Image Gen)" },
        { "automatic1111", "A1111 Stable Diffusion" },
        { "python", "Python (possible AI/ML script)" },
        { "python3", "Python3 (possible AI/ML script)" },
        { "pythonw", "Python (windowed - possible AI)" },
        { "node", "Node.js (possible AI extension)" },

        // Browser-based AI (via process name)
        { "msedge", "Microsoft Edge (Copilot / ChatGPT access)" },
        { "chrome", "Google Chrome (Gemini / ChatGPT access)" },
        { "firefox", "Firefox (AI tool access)" },

        // GitHub/FTP
        { "git", "Git (code sync - potential AI data)" },
        { "gh", "GitHub CLI" },
        { "GitHub Desktop", "GitHub Desktop (code sync)" },
        { "ftp", "FTP Client (data transfer)" },
        { "WinSCP", "WinSCP (file transfer)" },
        { "FileZilla", "FileZilla (FTP/SFTP)" },

        // Microsoft AI
        { "Copilot", "Microsoft Copilot" },
        { "WindowsCopilot", "Windows Copilot" },
        { "ai-assistant", "AI Assistant Process" },

        // SGL SyntheticAI
        { "SGL.JudgeDredd.App", "SGL SyntheticAI (This App)" },
    };

    public AiActivityViewModel()
    {
        Title = "AI Activity";
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        MonitorStatus = "Scanning for AI and LLM activity...";
        Activities.Clear();

        await ScanAiActivityAsync();

        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _monitorTimer.Tick += async (_, _) => await ScanAiActivityAsync();
        _monitorTimer.Start();
    }

    [RelayCommand]
    private void StopMonitoring()
    {
        _monitorTimer?.Stop();
        _monitorTimer = null;
        IsMonitoring = false;
        MonitorStatus = $"Monitoring stopped. {ActivityLog.Count} events logged.";
    }

    [RelayCommand]
    private void ClearLog()
    {
        ActivityLog.Clear();
    }

    [RelayCommand]
    private void KillProcess(AiActivityEntry? entry)
    {
        if (entry is null) return;
        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(entry.Pid);
            proc.Kill();
            proc.Dispose();
            entry.Status = "Killed";
            Activities.Remove(entry);
            MonitorStatus = $"Process {entry.ProcessName} (PID {entry.Pid}) terminated.";
        }
        catch (ArgumentException)
        {
            entry.Status = "Exited";
            MonitorStatus = $"Process {entry.ProcessName} already exited.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MonitorStatus = $"Access denied: Cannot kill {entry.ProcessName}. Run as administrator.";
        }
        catch (Exception ex)
        {
            MonitorStatus = $"Failed to kill {entry.ProcessName}: {ex.Message}";
        }
    }

    private async Task ScanAiActivityAsync()
    {
        await Task.Run(() =>
        {
            var processes = Process.GetProcesses();
            var found = new List<AiActivityEntry>();

            foreach (var proc in processes)
            {
                try
                {
                    if (KnownAiProcesses.TryGetValue(proc.ProcessName, out var description))
                    {
                        string modulePath = "Unknown";
                        try { modulePath = proc.MainModule?.FileName ?? "Unknown"; } catch { }

                        // Determine what data might be accessed
                        string dataAccess = DetermineDataAccess(proc.ProcessName);

                        var entry = new AiActivityEntry
                        {
                            Pid = proc.Id,
                            ProcessName = proc.ProcessName,
                            Description = description,
                            ModulePath = modulePath,
                            DataAccessInfo = dataAccess,
                            DetectedAt = DateTime.Now,
                            MemoryUsage = FormatBytes(proc.WorkingSet64),
                            ThreadCount = proc.Threads.Count,
                            CpuTime = proc.TotalProcessorTime.ToString(@"hh\:mm\:ss"),
                            Status = "Running",
                        };

                        found.Add(entry);
                    }
                }
                catch { /* Access denied or exited */ }
            }

            foreach (var p in processes) { try { p.Dispose(); } catch { } }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // Update active list
                Activities.Clear();
                foreach (var e in found)
                {
                    Activities.Add(e);

                    // Add to log if not duplicate within 30 seconds
                    if (!ActivityLog.Any(h => h.Pid == e.Pid && h.ProcessName == e.ProcessName
                                              && (DateTime.Now - h.DetectedAt).TotalSeconds < 30))
                    {
                        ActivityLog.Insert(0, e);
                        while (ActivityLog.Count > 1000)
                            ActivityLog.RemoveAt(ActivityLog.Count - 1);
                    }
                }

                AiProcessesDetected = Activities.Count;
                MonitorStatus = $"Monitoring active - {AiProcessesDetected} AI process(es) detected - {DateTime.Now:HH:mm:ss}";
            });
        });
    }

    private static string DetermineDataAccess(string processName)
    {
        var lower = processName.ToLowerInvariant();

        if (lower.Contains("ollama") || lower.Contains("llama") || lower.Contains("kobold") ||
            lower.Contains("lmstudio") || lower.Contains("gpt4all") || lower.Contains("jan") ||
            lower.Contains("vllm") || lower.Contains("localai"))
            return "Local model inference - may read local files, clipboard, system info";

        if (lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("firefox"))
            return "Browser-based AI access - browsing data, cookies, input text sent to cloud";

        if (lower.Contains("python") || lower.Contains("node"))
            return "Script execution - may access filesystem, network, clipboard, system APIs";

        if (lower.Contains("git") || lower.Contains("gh") || lower.Contains("github"))
            return "Code repository sync - source code, credentials, configs may be transmitted";

        if (lower.Contains("ftp") || lower.Contains("winscp") || lower.Contains("filezilla"))
            return "File transfer - files uploaded/downloaded to/from remote server";

        if (lower.Contains("copilot"))
            return "Microsoft AI Copilot - keyboard input, screen content, file contents analyzed";

        if (lower.Contains("stable") || lower.Contains("comfy") || lower.Contains("automatic"))
            return "Image generation AI - prompts and generated content processed locally";

        if (lower.Contains("sgl.judgedredd"))
            return "SGL SyntheticAI - local AI model, no cloud data transmission";

        return "AI/LLM activity detected - monitoring data access patterns";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}

public class AiActivityEntry
{
    public int Pid { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModulePath { get; set; } = string.Empty;
    public string DataAccessInfo { get; set; } = string.Empty;
    public string MemoryUsage { get; set; } = string.Empty;
    public int ThreadCount { get; set; }
    public string CpuTime { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
