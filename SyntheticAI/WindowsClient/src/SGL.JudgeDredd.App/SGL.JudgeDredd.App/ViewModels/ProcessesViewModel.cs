using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class ProcessesViewModel : ViewModelBase
{
    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss",
        "lsass",
        "svchost",
        "smss",
        "winlogon",
        "services",
        "System",
        "wininit",
        "dwm"
    };

    private readonly DispatcherTimer _autoRefreshTimer;

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _processes = [];

    [ObservableProperty]
    private ProcessInfo? _selectedProcess;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private int _totalProcesses;

    [ObservableProperty]
    private long _totalMemoryUsage;

    [ObservableProperty]
    private string _totalMemoryDisplay = "0 B";

    [ObservableProperty]
    private bool _autoRefreshEnabled;

    [ObservableProperty]
    private string _sortBy = "Name";

    [ObservableProperty]
    private string _top5ResourceHeavy = string.Empty;

    public ProcessesViewModel()
    {
        Title = "Processes";

        _autoRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _autoRefreshTimer.Tick += async (_, _) => await RefreshAsync();

        RefreshCommand.ExecuteAsync(null);
    }

    partial void OnSearchFilterChanged(string value)
    {
        RefreshCommand.ExecuteAsync(null);
    }

    partial void OnSortByChanged(string value)
    {
        RefreshCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void SortByCpu()
    {
        SortBy = "CPU";
    }

    [RelayCommand]
    private void SortByMemory()
    {
        SortBy = "Memory";
    }

    [RelayCommand]
    private void SortByName()
    {
        SortBy = "Name";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;

        try
        {
            var processInfos = await Task.Run(() => CollectProcessInformation());

            Processes.Clear();
            foreach (var info in processInfos)
            {
                Processes.Add(info);
            }

            TotalProcesses = Processes.Count;
            TotalMemoryUsage = Processes.Sum(p => p.MemoryBytes);
            TotalMemoryDisplay = FormatBytes(TotalMemoryUsage);

            // Build top 5 resource heavy apps
            var top5 = Processes.OrderByDescending(p => p.MemoryBytes).Take(5).ToList();
            if (top5.Count > 0)
            {
                var lines = top5.Select((p, i) => $"{i + 1}. {p.Name} - {p.MemoryDisplay} ({p.CpuUsage:F1}% CPU)");
                Top5ResourceHeavy = "Top 5 Resource Heavy: " + string.Join(" | ", lines);
            }
        }
        catch (Exception)
        {
            // Silently handle unexpected errors during refresh
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private List<ProcessInfo> CollectProcessInformation()
    {
        var results = new List<ProcessInfo>();
        var processes = Process.GetProcesses();
        var processorCount = Environment.ProcessorCount;

        // First snapshot of CPU times
        var cpuSnapshot1 = new Dictionary<int, TimeSpan>();
        foreach (var proc in processes)
        {
            try
            {
                cpuSnapshot1[proc.Id] = proc.TotalProcessorTime;
            }
            catch (Exception)
            {
                // Access denied or process exited
            }
        }

        // Wait 500ms for CPU measurement
        System.Threading.Thread.Sleep(500);

        // Second snapshot and build results
        foreach (var proc in processes)
        {
            try
            {
                var name = proc.ProcessName;

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchFilter) &&
                    !name.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double cpuUsage = 0.0;
                if (cpuSnapshot1.TryGetValue(proc.Id, out var startCpu))
                {
                    try
                    {
                        var endCpu = proc.TotalProcessorTime;
                        var cpuDelta = (endCpu - startCpu).TotalMilliseconds;
                        cpuUsage = cpuDelta / 500.0 / processorCount * 100.0;
                        cpuUsage = Math.Round(Math.Max(0, cpuUsage), 1);
                    }
                    catch (Exception)
                    {
                        // Process may have exited or access denied
                    }
                }

                long memoryBytes = proc.WorkingSet64;
                int threadCount = proc.Threads.Count;

                var info = new ProcessInfo
                {
                    Pid = proc.Id,
                    Name = name,
                    CpuUsage = cpuUsage,
                    MemoryBytes = memoryBytes,
                    MemoryDisplay = FormatBytes(memoryBytes),
                    ThreadCount = threadCount,
                    IsCritical = CriticalProcessNames.Contains(name)
                };

                results.Add(info);
            }
            catch (Exception)
            {
                // Access denied or process exited between enumeration; skip it
            }
        }

        // Dispose process handles
        foreach (var proc in processes)
        {
            try
            {
                proc.Dispose();
            }
            catch (Exception)
            {
                // Ignore dispose errors
            }
        }

        return SortBy switch
        {
            "CPU" => results.OrderByDescending(p => p.CpuUsage).ToList(),
            "Memory" => results.OrderByDescending(p => p.MemoryBytes).ToList(),
            _ => results.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    [RelayCommand]
    private async Task KillProcessAsync(ProcessInfo? processInfo)
    {
        if (processInfo is null)
            return;

        if (processInfo.IsCritical)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Cannot terminate '{processInfo.Name}' - it is a critical system process. Killing it could crash your system.");
            return;
        }

        try
        {
            var process = Process.GetProcessById(processInfo.Pid);
            process.Kill();
            process.Dispose();

            Processes.Remove(processInfo);
            TotalProcesses = Processes.Count;
            TotalMemoryUsage = Processes.Sum(p => p.MemoryBytes);
            TotalMemoryDisplay = FormatBytes(TotalMemoryUsage);

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Process '{processInfo.Name}' (PID {processInfo.Pid}) has been terminated.");
        }
        catch (ArgumentException)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Process '{processInfo.Name}' (PID {processInfo.Pid}) is no longer running.");
            Processes.Remove(processInfo);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Access denied. Cannot terminate '{processInfo.Name}'. Try running as administrator.");
        }
        catch (InvalidOperationException)
        {
            await AvatarViewModel.Instance.ShowSpeechBubble(
                $"Process '{processInfo.Name}' has already exited.");
            Processes.Remove(processInfo);
        }
    }

    [RelayCommand]
    private async Task AutoRefreshAsync()
    {
        AutoRefreshEnabled = !AutoRefreshEnabled;

        if (AutoRefreshEnabled)
        {
            _autoRefreshTimer.Start();
            await AvatarViewModel.Instance.ShowSpeechBubble("Auto-refresh enabled. Updating every 2 seconds.");
        }
        else
        {
            _autoRefreshTimer.Stop();
            await AvatarViewModel.Instance.ShowSpeechBubble("Auto-refresh disabled.");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "0 B";

        if (bytes < 1024)
            return $"{bytes} B";

        if (bytes < 1024L * 1024)
            return $"{bytes / 1024.0:F1} KB";

        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";

        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    public partial class ProcessInfo : ObservableObject
    {
        [ObservableProperty]
        private int _pid;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private double _cpuUsage;

        [ObservableProperty]
        private long _memoryBytes;

        [ObservableProperty]
        private string _memoryDisplay = string.Empty;

        [ObservableProperty]
        private int _threadCount;

        [ObservableProperty]
        private bool _isCritical;
    }
}
