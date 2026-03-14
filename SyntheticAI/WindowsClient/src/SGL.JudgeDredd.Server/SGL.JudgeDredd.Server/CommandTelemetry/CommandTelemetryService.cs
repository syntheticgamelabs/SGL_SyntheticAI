#pragma warning disable CA1416 // Platform compatibility warnings suppressed; this suite targets Windows desktop only.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using SGL.JudgeDredd.Shared.Logging;

namespace SGL.JudgeDredd.Server.CommandTelemetry;

/// <summary>
/// Server-side command dispatch and telemetry ingestion service for the SGL
/// SyntheticAI Security Suite. Maintains per-client command queues, stores
/// latest telemetry snapshots, tracks command lifecycle (Pending -> Sent ->
/// Completed/Failed), and provides a full audit log for accountability.
/// </summary>
public sealed class CommandTelemetryService
{
    // -------------------------------------------------------------------
    //  Nested models
    // -------------------------------------------------------------------

    public class RemoteCommand
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ClientId { get; set; } = ""; // empty = broadcast
        public string CommandType { get; set; } = ""; // Scan, Update, Config, Restart, CustomScript
        public string Payload { get; set; } = "";
        public string Status { get; set; } = "Pending"; // Pending, Sent, Completed, Failed
        public string Result { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public class ClientTelemetry
    {
        public string ClientId { get; set; } = "";
        public string MachineName { get; set; } = "";
        public double CpuPercent { get; set; }
        public double MemoryPercent { get; set; }
        public long DiskFreeGb { get; set; }
        public int ThreatsDetected { get; set; }
        public int FilesScanned { get; set; }
        public string ScanStatus { get; set; } = "Idle";
        public string AppVersion { get; set; } = "";
        public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; } = true;
    }

    // -------------------------------------------------------------------
    //  Internal audit record
    // -------------------------------------------------------------------

    private class AuditEntry
    {
        public string CommandId { get; init; } = "";
        public string ClientId { get; init; } = "";
        public string CommandType { get; init; } = "";
        public string Action { get; init; } = ""; // Enqueued, Sent, Completed, Failed
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string Details { get; init; } = "";
    }

    // -------------------------------------------------------------------
    //  State
    // -------------------------------------------------------------------

    /// <summary>
    /// Per-client pending command queues. The key is the client identifier.
    /// </summary>
    private readonly ConcurrentDictionary<string, Queue<RemoteCommand>> _commandQueues = new();

    /// <summary>
    /// Latest telemetry snapshot per client. The key is the client identifier.
    /// </summary>
    private readonly ConcurrentDictionary<string, ClientTelemetry> _clientTelemetry = new();

    /// <summary>
    /// All commands ever created, keyed by command ID for fast result recording.
    /// </summary>
    private readonly ConcurrentDictionary<string, RemoteCommand> _commandIndex = new();

    /// <summary>Immutable audit trail of all command lifecycle events.</summary>
    private readonly ConcurrentBag<AuditEntry> _auditLog = new();

    /// <summary>Lock for observable collection mutations.</summary>
    private readonly object _lock = new();

    /// <summary>Timer that periodically marks clients as offline if they miss heartbeats.</summary>
    private readonly Timer _heartbeatTimer;

    /// <summary>How long before a client is considered offline.</summary>
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(2);

    // -------------------------------------------------------------------
    //  Observable state
    // -------------------------------------------------------------------

    /// <summary>Chronological history of all commands.</summary>
    public ObservableCollection<RemoteCommand> CommandHistory { get; } = new();

    /// <summary>All known client telemetry snapshots.</summary>
    public ObservableCollection<ClientTelemetry> ClientTelemetryData { get; } = new();

    /// <summary>Number of commands in Pending or Sent status.</summary>
    public int PendingCommands { get; private set; }

    /// <summary>Number of commands in Completed status.</summary>
    public int CompletedCommands { get; private set; }

    /// <summary>Number of clients that have sent telemetry and are currently online.</summary>
    public int ConnectedClients { get; private set; }

    // -------------------------------------------------------------------
    //  Constructor
    // -------------------------------------------------------------------

    public CommandTelemetryService()
    {
        // Check for stale heartbeats every 30 seconds.
        _heartbeatTimer = new Timer(HeartbeatCheckCallback, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        SglLogger.Information("[CommandTelemetry] Service initialized.");
    }

    // -------------------------------------------------------------------
    //  Command dispatch
    // -------------------------------------------------------------------

    /// <summary>
    /// Enqueues a command targeted at a specific client. The client will
    /// retrieve pending commands on its next poll via
    /// <see cref="GetPendingCommands"/>.
    /// </summary>
    public void EnqueueCommand(string clientId, RemoteCommand cmd)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID must not be empty.", nameof(clientId));
        if (cmd == null)
            throw new ArgumentNullException(nameof(cmd));

        cmd.ClientId = clientId;
        cmd.Status = "Pending";
        cmd.CreatedAt = DateTime.UtcNow;

        // Ensure the queue exists and enqueue the command.
        Queue<RemoteCommand> queue = _commandQueues.GetOrAdd(clientId, _ => new Queue<RemoteCommand>());
        lock (queue)
        {
            queue.Enqueue(cmd);
        }

        // Index the command for result tracking.
        _commandIndex[cmd.Id] = cmd;

        // Record in observable history.
        lock (_lock)
        {
            CommandHistory.Add(cmd);
            RecalculateCounters();
        }

        // Audit log entry.
        WriteAudit(cmd.Id, clientId, cmd.CommandType, "Enqueued",
            $"Payload length: {cmd.Payload?.Length ?? 0} chars");

        SglLogger.Information(
            "[CommandTelemetry] Command {CmdId} ({Type}) enqueued for client {Client}",
            cmd.Id, cmd.CommandType, clientId);
    }

    /// <summary>
    /// Broadcasts a command to ALL known clients. A copy of the command is
    /// enqueued in every client's queue.
    /// </summary>
    public void BroadcastCommand(RemoteCommand cmd)
    {
        if (cmd == null)
            throw new ArgumentNullException(nameof(cmd));

        cmd.ClientId = ""; // broadcast marker
        cmd.Status = "Pending";
        cmd.CreatedAt = DateTime.UtcNow;

        // Index the template command.
        _commandIndex[cmd.Id] = cmd;

        lock (_lock)
        {
            CommandHistory.Add(cmd);
        }

        WriteAudit(cmd.Id, "*", cmd.CommandType, "Broadcast",
            $"Broadcasting to {_clientTelemetry.Count} known client(s)");

        // Create per-client copies.
        foreach (string clientId in _clientTelemetry.Keys)
        {
            var clientCopy = new RemoteCommand
            {
                Id = $"{cmd.Id}-{clientId}",
                ClientId = clientId,
                CommandType = cmd.CommandType,
                Payload = cmd.Payload,
                Status = "Pending",
                CreatedAt = cmd.CreatedAt,
            };

            Queue<RemoteCommand> queue = _commandQueues.GetOrAdd(clientId, _ => new Queue<RemoteCommand>());
            lock (queue)
            {
                queue.Enqueue(clientCopy);
            }

            _commandIndex[clientCopy.Id] = clientCopy;

            lock (_lock)
            {
                CommandHistory.Add(clientCopy);
            }

            WriteAudit(clientCopy.Id, clientId, clientCopy.CommandType, "Enqueued (broadcast)",
                $"Child of broadcast command {cmd.Id}");
        }

        lock (_lock)
        {
            RecalculateCounters();
        }

        SglLogger.Information(
            "[CommandTelemetry] Broadcast command {CmdId} ({Type}) to {Count} client(s)",
            cmd.Id, cmd.CommandType, _clientTelemetry.Count);
    }

    /// <summary>
    /// Returns and removes all pending commands for the specified client.
    /// The client should call this on each poll cycle.
    /// </summary>
    public Queue<RemoteCommand> GetPendingCommands(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return new Queue<RemoteCommand>();

        var result = new Queue<RemoteCommand>();

        if (_commandQueues.TryGetValue(clientId, out Queue<RemoteCommand>? queue))
        {
            lock (queue)
            {
                while (queue.Count > 0)
                {
                    RemoteCommand cmd = queue.Dequeue();
                    cmd.Status = "Sent";

                    WriteAudit(cmd.Id, clientId, cmd.CommandType, "Sent",
                        "Command dequeued and dispatched to client");

                    result.Enqueue(cmd);
                }
            }

            lock (_lock)
            {
                RecalculateCounters();
            }
        }

        if (result.Count > 0)
        {
            SglLogger.Information(
                "[CommandTelemetry] Dispatched {Count} command(s) to client {Client}",
                result.Count, clientId);
        }

        return result;
    }

    // -------------------------------------------------------------------
    //  Telemetry ingestion
    // -------------------------------------------------------------------

    /// <summary>
    /// Records a telemetry snapshot from a client. Updates the latest data
    /// and refreshes the observable collection.
    /// </summary>
    public void RecordTelemetry(string clientId, ClientTelemetry data)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID must not be empty.", nameof(clientId));
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        data.ClientId = clientId;
        data.LastHeartbeat = DateTime.UtcNow;
        data.IsOnline = true;

        _clientTelemetry.AddOrUpdate(clientId, data, (_, _) => data);

        // Refresh the observable collection.
        lock (_lock)
        {
            // Find existing entry and update, or add new.
            ClientTelemetry? existing = null;
            for (int i = 0; i < ClientTelemetryData.Count; i++)
            {
                if (ClientTelemetryData[i].ClientId == clientId)
                {
                    existing = ClientTelemetryData[i];
                    ClientTelemetryData[i] = data;
                    break;
                }
            }

            if (existing == null)
            {
                ClientTelemetryData.Add(data);
            }

            ConnectedClients = _clientTelemetry.Values.Count(c => c.IsOnline);
        }

        SglLogger.Debug(
            "[CommandTelemetry] Telemetry received from {Client} ({Machine}): " +
            "CPU={Cpu}%, Mem={Mem}%, Threats={Threats}, ScanStatus={Status}",
            clientId, data.MachineName, data.CpuPercent, data.MemoryPercent,
            data.ThreatsDetected, data.ScanStatus);
    }

    // -------------------------------------------------------------------
    //  Command result recording
    // -------------------------------------------------------------------

    /// <summary>
    /// Records the result (success or failure) of a previously dispatched
    /// command. Updates the command's status and result text.
    /// </summary>
    public void RecordCommandResult(string commandId, bool success, string result)
    {
        if (string.IsNullOrWhiteSpace(commandId))
            return;

        if (_commandIndex.TryGetValue(commandId, out RemoteCommand? cmd))
        {
            cmd.Status = success ? "Completed" : "Failed";
            cmd.Result = result ?? "";
            cmd.CompletedAt = DateTime.UtcNow;

            WriteAudit(cmd.Id, cmd.ClientId, cmd.CommandType,
                success ? "Completed" : "Failed",
                Truncate(result, 500));

            lock (_lock)
            {
                RecalculateCounters();
            }

            SglLogger.Information(
                "[CommandTelemetry] Command {CmdId} {Status}: {Result}",
                commandId, cmd.Status, Truncate(result, 200));
        }
        else
        {
            SglLogger.Warning(
                "[CommandTelemetry] RecordCommandResult called for unknown command {CmdId}",
                commandId);
        }
    }

    // -------------------------------------------------------------------
    //  Heartbeat / offline detection
    // -------------------------------------------------------------------

    private void HeartbeatCheckCallback(object? state)
    {
        DateTime threshold = DateTime.UtcNow - HeartbeatTimeout;
        bool changed = false;

        foreach (var kvp in _clientTelemetry)
        {
            ClientTelemetry ct = kvp.Value;
            if (ct.IsOnline && ct.LastHeartbeat < threshold)
            {
                ct.IsOnline = false;
                changed = true;

                SglLogger.Warning(
                    "[CommandTelemetry] Client {Client} ({Machine}) marked offline. " +
                    "Last heartbeat: {LastHeartbeat}",
                    ct.ClientId, ct.MachineName, ct.LastHeartbeat);
            }
        }

        if (changed)
        {
            lock (_lock)
            {
                // Refresh observable collection with updated online states.
                ClientTelemetryData.Clear();
                foreach (var ct in _clientTelemetry.Values)
                    ClientTelemetryData.Add(ct);

                ConnectedClients = _clientTelemetry.Values.Count(c => c.IsOnline);
            }
        }
    }

    // -------------------------------------------------------------------
    //  Counter maintenance
    // -------------------------------------------------------------------

    /// <summary>
    /// Recalculates PendingCommands and CompletedCommands from the command
    /// index. Must be called under <c>_lock</c>.
    /// </summary>
    private void RecalculateCounters()
    {
        int pending = 0;
        int completed = 0;

        foreach (RemoteCommand cmd in _commandIndex.Values)
        {
            switch (cmd.Status)
            {
                case "Pending":
                case "Sent":
                    pending++;
                    break;
                case "Completed":
                    completed++;
                    break;
            }
        }

        PendingCommands = pending;
        CompletedCommands = completed;
    }

    // -------------------------------------------------------------------
    //  Audit logging
    // -------------------------------------------------------------------

    private void WriteAudit(string commandId, string clientId, string commandType,
        string action, string details)
    {
        var entry = new AuditEntry
        {
            CommandId = commandId,
            ClientId = clientId,
            CommandType = commandType,
            Action = action,
            Timestamp = DateTime.UtcNow,
            Details = details ?? "",
        };

        _auditLog.Add(entry);

        SglLogger.Information(
            "[CommandTelemetry:Audit] CmdId={CmdId} Client={Client} Type={Type} " +
            "Action={Action} Details={Details}",
            entry.CommandId, entry.ClientId, entry.CommandType,
            entry.Action, Truncate(entry.Details, 300));
    }

    // -------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
