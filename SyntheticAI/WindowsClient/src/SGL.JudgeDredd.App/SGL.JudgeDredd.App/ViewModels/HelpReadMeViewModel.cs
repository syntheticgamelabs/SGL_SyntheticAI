using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGL.JudgeDredd.App.ViewModels;

/// <summary>
/// ViewModel for the Help &amp; Documentation view. Provides a comprehensive
/// indexed guide covering every feature of the SGL SyntheticAI application,
/// with section-based navigation.
/// </summary>
public partial class HelpReadMeViewModel : ViewModelBase
{
    private readonly Dictionary<string, string> _sectionContent = new();
    private bool _isAdmin;

    [ObservableProperty]
    private string _helpContent = string.Empty;

    [ObservableProperty]
    private string _selectedSection = string.Empty;

    public ObservableCollection<string> Sections { get; } = [];

    // Admin-only section keys
    private static readonly HashSet<string> AdminOnlySections =
    [
        "13. Admin Panel"
    ];

    public HelpReadMeViewModel(bool isAdmin = false)
    {
        _isAdmin = isAdmin;
        Title = "Help & Documentation";
        BuildSectionContent();
        RebuildSections();
        if (Sections.Count > 0)
        {
            SelectedSection = Sections[0];
            HelpContent = _sectionContent.GetValueOrDefault(Sections[0], "");
        }
    }

    /// <summary>
    /// Call this when user logs in/out to show/hide admin sections.
    /// </summary>
    public void SetAdminMode(bool isAdmin)
    {
        _isAdmin = isAdmin;
        RebuildSections();
    }

    private void RebuildSections()
    {
        Sections.Clear();

        var allSections = new[]
        {
            "1. Overview",
            "2. AI Security Scanner",
            "3. AI Chat",
            "4. Firewall Management",
            "5. VPN Service",
            "6. Network Monitor",
            "7. Endpoint Protection",
            "8. Git Repository Hosting",
            "9. Broadcast Chat",
            "10. YARA Rule Engine",
            "11. Telemetry Detection",
            "12. QR Code Sharing",
            "13. Admin Panel",
            "14. Device Protection (Mobile)",
            "15. Bluetooth Scanner (Mobile)",
            "16. WiFi Scanner (Mobile)",
            "17. How to Use: Scanner",
            "18. How to Use: AI Chat",
            "19. How to Use: Firewall",
            "20. How to Use: VPN",
            "21. How to Use: YARA Rules",
            "22. How to Use: Admin Panel",
            "23. Settings",
            "24. Keyboard Shortcuts",
            "25. Troubleshooting",
            "26. About"
        };

        foreach (var section in allSections)
        {
            if (AdminOnlySections.Contains(section) && !_isAdmin)
                continue;
            Sections.Add(section);
        }
    }

    partial void OnSelectedSectionChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && _sectionContent.TryGetValue(value, out var content))
        {
            HelpContent = content;
        }
    }

    // ── Help content builder ─────────────────────────────────────────────

    private void BuildSectionContent()
    {
        _sectionContent["1. Overview"] = """
            SGL SYNTHETICAI - AI-Powered Security Suite BetaV1.1.28
            ══════════════════════════════════════════════

            SGL SyntheticAI is a comprehensive AI-powered security suite
            developed by Synthetic Game Labs. It combines traditional antivirus
            and firewall capabilities with cutting-edge artificial intelligence
            powered by a local LLM with a thinking UI, a JWT-authenticated
            API server, and a WebSocket gateway for real-time device communication.

            Platforms:
              - Windows Desktop (WPF / .NET 8)
              - Linux CLI (.NET 9)
              - Android Mobile (MAUI)
              - Web Dashboard (React)

            Built With:
              - .NET 8 / .NET 9
              - WPF (Windows Presentation Foundation)
              - .NET MAUI (Android mobile app)
              - React (web dashboard)
              - ASP.NET Core Kestrel (embedded API server)

            Core Features:
              1.  AI Security Scanner - Real-time file scanning with YARA rules,
                  quarantine vault, and threat actions (quarantine, ignore, watch with AI)
              2.  AI Chat - LLM-powered assistant with thinking UI (expandable
                  thought bubbles), text-to-speech with 9 voice options, and
                  knowledge library for offline learning
              3.  Firewall Management - Real Windows Firewall rule management
                  with full CRUD operations
              4.  VPN Service - WireGuard-based VPN management with kill switch
              5.  Network Monitor - Real-time network traffic analysis and
                  connection monitoring
              6.  Endpoint Protection - Behavioral anomaly detection for
                  processes, USB devices, and registry changes
              7.  Git Repository Hosting - Code hosting with file browsing
                  and download capabilities
              8.  Broadcast Chat - Blackboard messaging system with admin
                  notifications and countdown timers
              9.  YARA Rule Engine - Custom rule creation, import/export,
                  and pattern matching (hex, text, regex)
              10. Telemetry Detection - Identifies apps with suspicious
                  permissions and provides force-stop capability
              11. QR Code Sharing - Generate scannable QR codes for app
                  download links
              12. Admin Panel - Server metrics, user management (kick/ban/remove),
                  and broadcast notification capabilities
              13. Device Protection (Mobile) - Camera/mic blocker and app
                  permission scanning on Android
              14. Bluetooth Scanner (Mobile) - Real device discovery with
                  threat analysis and connection management
              15. WiFi Scanner (Mobile) - Real network scanning with
                  connection testing and trace route

            Local LLM Integration:
              - Runs a local reasoning LLM for AI-powered threat analysis
              - Thinking UI with expandable thought bubbles shows the AI's
                chain-of-thought reasoning process in real time
              - No cloud dependency for core AI features (privacy-first)
              - Streaming token output for responsive interactions

            Server & Connectivity:
              - JWT token-based authentication for all API endpoints
              - REST API endpoints for login, registration, heartbeat, and commands
              - WebSocket gateway for real-time desktop-to-mobile communication
              - Cloudflare tunnel provides public access via syntheticgamelabs.dpdns.org

            The application runs on Windows, Linux, and Android, providing
            maximum protection with minimal performance impact across all
            supported platforms.
            """;

        _sectionContent["2. AI Security Scanner"] = """
            AI SECURITY SCANNER
            ══════════════════════════════════════════════

            The AI Security Scanner is the core antivirus engine with real-time
            file scanning, YARA rule integration, and intelligent threat actions.

            Scan Types:
              - Quick Scan: Critical directories and startup locations
              - Full Scan: Exhaustive scan of all files on all drives
              - Custom Scan: Select specific files, folders, or drives

            YARA Rule Integration:
              - Scans files against custom and built-in YARA rules
              - Pattern matching supports hex, text, and regex patterns
              - Rules are applied alongside signature-based detection
              - Custom rules can target organization-specific threats

            Quarantine Vault:
              - Detected threats are isolated in an AES-256 encrypted vault
              - Files are safely contained and cannot execute
              - Original file path and metadata are preserved
              - Restore false positives back to their original location
              - Permanently delete confirmed threats from the vault

            Threat Actions:
              When a threat is detected, you have three options:
              - Quarantine: Encrypt and isolate the file in the quarantine vault
              - Ignore: Mark the file as safe and skip future detections
              - Watch with AI: Flag the file for ongoing AI behavioral monitoring;
                the LLM continuously evaluates the file's activity for suspicious
                behavior patterns

            Real-Time Protection:
              - FileSystemWatcher monitors file system changes in real time
              - New and modified files are automatically scanned
              - Color-coded results: Green = Safe, Red = Threat
              - Live progress bar with ETA and file count
              - Cancel running scans at any time

            Detection Methods:
              - Signature-based detection (250+ real malware signatures)
              - Heuristic analysis with PE header and import table scoring
              - YARA rule pattern matching
              - AI-powered behavioral evaluation via local LLM
              - Ransomware-specific detection (crypto API, shadow copy, etc.)
            """;

        _sectionContent["3. AI Chat"] = """
            AI CHAT
            ══════════════════════════════════════════════

            LLM-powered security assistant with a thinking UI, text-to-speech,
            and an offline knowledge library.

            Thinking UI:
              - The AI's chain-of-thought reasoning is displayed in expandable
                thought bubbles alongside each response
              - Click to expand or collapse the thinking process
              - Watch the AI reason through security questions in real time
              - Provides transparency into how the AI arrives at its answers

            Text-to-Speech (TTS):
              - Built-in TTS engine reads AI responses aloud
              - 9 voice options to choose from
              - Toggle TTS on/off per message or globally
              - Useful for hands-free security briefings

            Knowledge Library:
              - Offline learning resource built into the chat system
              - Curated security knowledge base for reference
              - AI can draw on the library for informed responses
              - Learn about security concepts without an internet connection

            General Usage:
              Type messages and press Enter or click Send. The AI can answer
              questions about security, system status, threats, and computing.
              Responses stream in real time with token-by-token output.

            Special Commands:
              /scan [target]    - Scan files or directories from chat
              /security [query] - Get security advice and guidance
              /code [request]   - Generate, explain, or review code
              /image [desc]     - Generate security-themed graphics

            The AI loads automatically on startup with a progress indicator.
            It has access to your system's security status for contextual advice.
            """;

        _sectionContent["4. Firewall Management"] = """
            FIREWALL MANAGEMENT
            ══════════════════════════════════════════════

            Graphical interface for managing real Windows Firewall rules directly.

            Features:
              - View status for Domain, Private, and Public profiles
              - Enable/Disable firewall per profile
              - Create, edit, and delete firewall rules
              - Rules sync directly with Windows Firewall and persist across restarts

            Rule Creation:
              - Rule Name: Descriptive identifier for the rule
              - Direction: Inbound or Outbound traffic
              - Action: Allow or Block
              - Protocol: TCP, UDP, or Any
              - Port: Specific port number or range
              - Program Path: Target a specific application executable
              - Remote Address: Target a specific IP address or range

            Quick Actions:
              - Quick Block: Instantly block active connections
              - Application Block: Block all traffic for a specific application
              - IP Block: Block traffic from a specific IP address

            All rules are applied directly via Windows Firewall (netsh) and
            take effect immediately. Changes persist across system restarts.
            """;

        _sectionContent["5. VPN Service"] = """
            VPN SERVICE
            ══════════════════════════════════════════════

            WireGuard-based VPN management for encrypted traffic and privacy.

            Features:
              - WireGuard protocol for fast, modern VPN connections
              - Server list with connection status indicators
              - Kill Switch: Block all internet traffic if VPN connection drops
              - DNS Leak Protection: Route DNS queries through the VPN tunnel
              - Connection Status: Real-time IP, uptime, and transfer statistics

            WireGuard Integration:
              - Uses the WireGuard protocol for high-performance encryption
              - Lightweight and efficient compared to legacy VPN protocols
              - Automatic key exchange and tunnel management
              - Low-latency connections with minimal overhead

            Privacy Features:
              - IP Masking: Your real IP address is hidden behind the VPN server
              - Encrypted Tunnel: All traffic is encrypted end-to-end
              - Kill Switch: Prevents data leaks if the VPN disconnects unexpectedly
              - DNS Leak Protection: Ensures DNS queries do not bypass the tunnel
            """;

        _sectionContent["6. Network Monitor"] = """
            NETWORK MONITOR
            ══════════════════════════════════════════════

            Real-time network traffic analysis and connection monitoring.

            Features:
              - Live view of all active network connections
              - Protocol breakdown (TCP, UDP) with connection states
              - Remote IP address identification with GeoIP lookups
              - Port monitoring for unusual or suspicious service activity
              - Connection rate tracking to detect scanning or brute force
              - Process-level network attribution (which app is connecting where)

            Detection Capabilities:
              - Connections to known malicious IP addresses
              - Port scan detection (multiple connections from same source)
              - Unusual port usage (uncommon services on standard ports)
              - Rapid connection attempts indicating automated attacks
              - Data exfiltration patterns (large outbound transfers)

            Alert Levels:
              - Critical: Connections to known malicious IPs
              - High: Detected port scanning or brute force attempts
              - Medium: Unusual port usage or unexpected services
              - Low: Informational connection activity

            Actions:
              - Block IP: Create a firewall rule to block suspicious addresses
              - Allow: Whitelist trusted connections
              - Export: Save network activity reports for analysis
            """;

        _sectionContent["7. Endpoint Protection"] = """
            ENDPOINT PROTECTION
            ══════════════════════════════════════════════

            Behavioral anomaly detection across multiple system vectors.

            Monitoring Vectors:
              - Process Monitoring: Tracks process behavior, detects injection,
                privilege escalation, and suspicious child processes
              - Network Monitoring: Active connections with malicious IP detection
              - USB Detection: Alerts on device connect/disconnect with auto-scan
                of newly connected removable media
              - Registry Watching: Monitors critical registry keys, startup entries,
                and persistence mechanisms used by malware

            AI-Powered Anomaly Detection:
              - Machine learning-based behavioral analysis
              - Confidence scores for detected anomalies
              - Baseline learning of normal system behavior
              - Alerts when processes deviate from expected patterns

            Ransomware Protection:
              - Monitors for crypto API usage patterns
              - Detects mass file enumeration and encryption
              - Shadow copy deletion detection
              - 40+ ransomware file extension monitoring in real time

            Each monitoring subsystem can be enabled or disabled independently.
            Alerts are categorized as Info, Warning, or Critical severity.
            """;

        _sectionContent["8. Git Repository Hosting"] = """
            GIT REPOSITORY HOSTING
            ══════════════════════════════════════════════

            Built-in code hosting service with file browsing and download
            capabilities.

            Features:
              - Host Git repositories directly from the SyntheticAI server
              - Browse repository files and directories through the UI
              - View file contents with syntax-appropriate display
              - Download individual files or entire repositories
              - Repository listing with metadata (size, last updated)

            Use Cases:
              - Share security tools and scripts within your organization
              - Host YARA rules and signature files for team distribution
              - Distribute configuration files and deployment scripts
              - Centralized code repository for security automation

            The Git hosting service integrates with the SyntheticAI server
            infrastructure and uses the same JWT authentication for access
            control.
            """;

        _sectionContent["9. Broadcast Chat"] = """
            BROADCAST CHAT
            ══════════════════════════════════════════════

            Blackboard-style messaging system with admin broadcast notifications.

            Features:
              - Blackboard messaging: Post messages visible to all connected users
              - Real-time message delivery via WebSocket gateway
              - Message history with timestamps and sender identification
              - Support for text-based communication between connected clients

            Admin Notifications:
              - Administrators can send broadcast notifications to all users
              - Countdown timers on notifications for time-sensitive alerts
              - Notifications appear as prominent banners across all clients
              - Use for maintenance windows, security alerts, or announcements

            Use Cases:
              - Security incident communication across the organization
              - Scheduled maintenance announcements with countdown timers
              - Real-time threat advisories pushed to all connected devices
              - Team coordination during security events
            """;

        _sectionContent["10. YARA Rule Engine"] = """
            YARA RULE ENGINE
            ══════════════════════════════════════════════

            Create, manage, and deploy custom YARA rules for flexible and
            precise threat detection.

            What Are YARA Rules:
              YARA rules are pattern-matching rules that identify malware based
              on textual or binary patterns found in files. They provide more
              flexibility than simple hash-based signatures and can target
              specific malware families or techniques.

            Pattern Matching Types:
              - Hex Patterns: Match raw byte sequences in binary files
                Example: { 4D 5A 90 00 } matches PE file headers
              - Text Patterns: Match ASCII or Unicode string content
                Example: "cmd.exe /c" matches command execution strings
              - Regex Patterns: Use regular expressions for flexible matching
                Example: /https?:\/\/[a-z0-9]+\.evil\.com/ matches malicious URLs

            Rule Management:
              - Create custom rules with a guided editor
              - Import rules from .yar files or community rule sets
              - Export your rules for sharing or backup
              - Enable/disable individual rules without deleting them
              - Test rules against sample files before deployment

            Rule Structure:
              - Rule Name: Unique identifier for the rule
              - Strings: Patterns to search for (hex, text, or regex)
              - Condition: Logic for matching (all of them, any of them, etc.)
              - Metadata: Author, description, severity, creation date

            The YARA engine runs during all scan types and integrates with
            the AI Security Scanner for comprehensive threat detection.
            """;

        _sectionContent["11. Telemetry Detection"] = """
            TELEMETRY DETECTION
            ══════════════════════════════════════════════

            Identifies applications with suspicious permissions and provides
            the ability to force-stop offending processes.

            Features:
              - Scans installed and running applications for telemetry behavior
              - Identifies apps with suspicious permission requests
              - Detects applications that phone home to tracking servers
              - Monitors for unauthorized data collection activities
              - Categorizes apps by telemetry risk level

            Suspicious Permission Indicators:
              - Network access without clear user-facing need
              - Camera or microphone access by non-media applications
              - File system access beyond the application's scope
              - Background execution with persistent network connections
              - Access to sensitive system information or hardware IDs

            Actions:
              - Force Stop: Immediately terminate a suspicious application
              - View Details: See full permission list and network activity
              - Allow: Mark an application as trusted
              - Monitor: Flag for ongoing observation

            Telemetry detection runs in the background and alerts you when
            applications exhibit suspicious data collection behavior.
            """;

        _sectionContent["12. QR Code Sharing"] = """
            QR CODE SHARING
            ══════════════════════════════════════════════

            Generate scannable QR codes for sharing app download links and
            other resources.

            Features:
              - Generate QR codes for the SyntheticAI mobile app download link
              - Create QR codes for custom URLs or text content
              - High-resolution QR code output suitable for printing or sharing
              - Scan QR codes directly from the mobile companion app

            Use Cases:
              - Share the Android mobile app download link with team members
              - Distribute server connection URLs for quick mobile setup
              - Generate QR codes for security resource links
              - Quick device onboarding by scanning a setup QR code

            QR codes can be saved as images or displayed on screen for
            immediate scanning by mobile devices.
            """;

        _sectionContent["13. Admin Panel"] = """
            ADMIN PANEL (Admin Users Only)
            ══════════════════════════════════════════════

            Comprehensive management tools for multi-user deployments. Only
            accessible to users with Admin privileges.

            Server Metrics:
              - Real-time server health and performance indicators
              - Connected client count and status overview
              - CPU, memory, and disk usage of the server
              - API request rates and response times
              - Uptime tracking and service availability

            User Management:
              - View all registered users in a detailed list
              - User details: ID, username, email, IP, client version, platform
              - Kick: Disconnect a user's active session immediately
              - Ban: Permanently block a user from connecting
              - Remove: Delete a user account entirely
              - Activate/Deactivate user accounts
              - Password change for any user (admin only, PBKDF2 hashed)
              - See which version and platform each user is running

            Broadcast Notifications:
              - Send broadcast messages to all connected users
              - Attach countdown timers to time-sensitive notifications
              - Notifications appear as banners across all client applications
              - Schedule maintenance windows and security advisories

            Diagnostics:
              - System Information: OS, machine name, processors, memory, uptime
              - Application Status: LLM model status, database size,
                threat signature count, registered user count
              - Client Connection Info: Local IP, public IP
              - Diagnostic Log: Detailed timestamped system log

            Default admin accounts:
              - Contact your administrator for credentials.
            """;

        _sectionContent["14. Device Protection (Mobile)"] = """
            DEVICE PROTECTION (Mobile)
            ══════════════════════════════════════════════

            Camera/microphone blocker and app permission scanning on Android
            mobile devices via the SyntheticAI MAUI companion app.

            Camera & Microphone Blocker:
              - Block camera access at the system level
              - Block microphone access at the system level
              - Toggle blocking on/off with a single tap
              - Visual indicator showing current block status
              - Prevents any application from accessing camera or mic
                while the blocker is active

            App Permission Scanning:
              - Scan all installed apps for their declared permissions
              - Identify apps with dangerous or excessive permissions
              - Categorize permissions by risk level (normal, dangerous, special)
              - Flag apps that request camera, microphone, location, contacts,
                or storage permissions unnecessarily
              - View detailed permission breakdown per application

            Use Cases:
              - Ensure privacy during sensitive meetings or in secure areas
              - Audit which apps have access to your camera and microphone
              - Identify potentially malicious apps requesting excessive permissions
              - Quickly lock down device sensors when not in use
            """;

        _sectionContent["15. Bluetooth Scanner (Mobile)"] = """
            BLUETOOTH SCANNER (Mobile)
            ══════════════════════════════════════════════

            Real Bluetooth device discovery with threat analysis and connection
            management on Android mobile devices.

            Device Discovery:
              - Scans for nearby Bluetooth and BLE (Bluetooth Low Energy) devices
              - Displays device name, MAC address, signal strength, and type
              - Real-time discovery updates as new devices are found
              - Categorizes devices by type (audio, computer, phone, peripheral, etc.)

            Threat Analysis:
              - Analyzes discovered devices for potential security risks
              - Identifies unknown or suspicious devices in range
              - Detects devices attempting to connect without authorization
              - Flags devices with names commonly associated with attack tools
              - Risk scoring based on device behavior and characteristics

            Connection Management:
              - View active Bluetooth connections
              - Monitor connection status and data transfer activity
              - Disconnect from suspicious devices
              - Block specific devices from connecting

            Use Cases:
              - Detect rogue Bluetooth devices in your environment
              - Identify potential Bluetooth-based attack vectors
              - Monitor for unauthorized device pairing attempts
              - Audit Bluetooth connections in secure areas
            """;

        _sectionContent["16. WiFi Scanner (Mobile)"] = """
            WIFI SCANNER (Mobile)
            ══════════════════════════════════════════════

            Real WiFi network scanning with connection testing and trace route
            capabilities on Android mobile devices.

            Network Scanning:
              - Scan for all available WiFi networks in range
              - Display SSID, BSSID, signal strength, security type, and channel
              - Identify open (unsecured) networks that pose security risks
              - Detect duplicate SSIDs that may indicate evil twin attacks
              - Real-time signal strength monitoring

            Connection Testing:
              - Test connectivity to specific networks
              - Measure latency and connection quality
              - Verify DNS resolution is functioning correctly
              - Check for captive portals and man-in-the-middle conditions

            Trace Route:
              - Trace the network path from your device to a target host
              - Visualize each hop with latency measurements
              - Identify network bottlenecks or suspicious routing
              - Useful for diagnosing connectivity issues and detecting
                traffic interception

            Use Cases:
              - Audit WiFi security in your environment
              - Detect rogue access points and evil twin attacks
              - Diagnose network connectivity issues
              - Verify secure routing of your network traffic
            """;

        _sectionContent["17. How to Use: Scanner"] = """
            HOW TO USE: AI SECURITY SCANNER
            ══════════════════════════════════════════════

            Getting Started:
              1. Navigate to the Scanner tab from the main navigation
              2. Choose your scan type: Quick, Full, or Custom
              3. For Custom scans, select the target files or folders
              4. Click "Start Scan" to begin

            During a Scan:
              - Watch the progress bar for completion percentage
              - The ETA countdown shows estimated time remaining
              - Current file being scanned is displayed in real time
              - Threats found counter updates as detections occur
              - Click "Cancel" at any time to stop the scan

            When Threats Are Found:
              - Detected threats appear in the results list highlighted in red
              - For each threat, choose an action:
                * Quarantine: Encrypts and isolates the file safely
                * Ignore: Marks as safe (use for known false positives)
                * Watch with AI: The LLM monitors the file's ongoing behavior
              - Quarantined files can be reviewed in the Quarantine Vault

            Best Practices:
              - Run a Quick Scan daily for routine protection
              - Run a Full Scan weekly for comprehensive coverage
              - Use Custom Scan when you download new files or software
              - Review quarantined items periodically
              - Create YARA rules for threats specific to your environment
            """;

        _sectionContent["18. How to Use: AI Chat"] = """
            HOW TO USE: AI CHAT
            ══════════════════════════════════════════════

            Starting a Conversation:
              1. Navigate to the Chat tab from the main navigation
              2. Wait for the LLM to finish loading (progress indicator shown)
              3. Type your message in the input box and press Enter or click Send

            Using the Thinking UI:
              - Each AI response may include a "thinking" section
              - Click on the thought bubble to expand and see the AI's reasoning
              - This shows the chain-of-thought process the AI used
              - Collapse the thinking section to focus on the final answer

            Text-to-Speech:
              - Click the speaker icon to have the AI read its response aloud
              - Choose from 9 different voice options in chat settings
              - Toggle TTS globally or per individual message
              - Useful for hands-free security briefings or accessibility

            Knowledge Library:
              - Access the knowledge library for offline learning resources
              - Browse curated security topics and reference material
              - The AI draws on this library for more informed responses

            Special Commands:
              - Type /scan followed by a file or folder path to trigger a scan
              - Type /security followed by a question for security guidance
              - Type /code followed by a request to generate or review code
              - Type /image followed by a description for security graphics

            Tips:
              - Ask about specific threats or security concepts
              - Request analysis of your current security posture
              - Use the AI to explain scan results or threat detections
              - The AI has access to your system's security status
            """;

        _sectionContent["19. How to Use: Firewall"] = """
            HOW TO USE: FIREWALL MANAGEMENT
            ══════════════════════════════════════════════

            Viewing Firewall Status:
              1. Navigate to the Firewall tab from the main navigation
              2. View the status of Domain, Private, and Public profiles
              3. Each profile shows whether the firewall is enabled or disabled

            Creating a New Rule:
              1. Click "New Rule" or the add button
              2. Enter a descriptive Rule Name
              3. Select Direction: Inbound (incoming traffic) or Outbound (outgoing)
              4. Select Action: Allow or Block
              5. Choose Protocol: TCP, UDP, or Any
              6. Specify Port number (optional)
              7. Specify Program Path to target a specific application (optional)
              8. Specify Remote Address to target a specific IP (optional)
              9. Click "Save" to apply the rule immediately

            Using Quick Actions:
              - Quick Block: Select an active connection and click to block instantly
              - Application Block: Browse to an executable to block all its traffic
              - IP Block: Enter an IP address to block all traffic from that source

            Managing Existing Rules:
              - View all rules in the rule list with filtering and search
              - Edit rules by selecting them and modifying properties
              - Delete rules that are no longer needed
              - All changes take effect immediately via Windows Firewall

            Note: Firewall management requires administrator privileges.
            Run SyntheticAI as administrator for full firewall control.
            """;

        _sectionContent["20. How to Use: VPN"] = """
            HOW TO USE: VPN SERVICE
            ══════════════════════════════════════════════

            Connecting to a VPN:
              1. Navigate to the VPN tab from the main navigation
              2. Browse the available server list
              3. Select a server and click "Connect"
              4. Wait for the WireGuard tunnel to establish
              5. Connection status shows your new IP and transfer statistics

            Enabling Kill Switch:
              1. Toggle the Kill Switch option before or after connecting
              2. When enabled, all internet traffic is blocked if VPN drops
              3. This prevents data leaks during unexpected disconnections
              4. Disable the kill switch when you want to disconnect normally

            DNS Leak Protection:
              - Enable DNS leak protection in the VPN settings
              - This routes all DNS queries through the VPN tunnel
              - Prevents your ISP from seeing which websites you visit

            Disconnecting:
              - Click "Disconnect" to close the VPN tunnel
              - If kill switch is enabled, disable it first or traffic will be blocked
              - Your original IP address will be restored after disconnecting

            Best Practices:
              - Always enable the kill switch on public or untrusted WiFi
              - Use DNS leak protection for maximum privacy
              - Check your connection status periodically
              - Choose servers geographically close to you for best performance
            """;

        _sectionContent["21. How to Use: YARA Rules"] = """
            HOW TO USE: YARA RULE ENGINE
            ══════════════════════════════════════════════

            Creating a New Rule:
              1. Navigate to the YARA Rules tab
              2. Click "New Rule" to open the rule editor
              3. Enter a unique Rule Name
              4. Add one or more string patterns:
                 - Text: Plain text strings to match (e.g., "malware_payload")
                 - Hex: Byte sequences in hex format (e.g., { 4D 5A 90 00 })
                 - Regex: Regular expression patterns (e.g., /http[s]?:\/\/.*\.xyz/)
              5. Set the Condition: "all of them", "any of them", or count-based
              6. Add metadata: author, description, severity, date
              7. Click "Save" to add the rule to the engine

            Importing Rules:
              1. Click "Import" in the YARA Rules tab
              2. Select a .yar file or paste rule text
              3. Imported rules are validated and added to the rule set
              4. Enable or disable imported rules as needed

            Exporting Rules:
              1. Select rules you want to export
              2. Click "Export" and choose a save location
              3. Rules are saved in standard YARA format (.yar)
              4. Share exported rules with team members or other deployments

            Testing Rules:
              - Select a rule and click "Test"
              - Choose a sample file to test against
              - The engine reports whether the rule matches
              - Refine patterns based on test results

            Tips:
              - Start with text patterns for known malware strings
              - Use hex patterns for binary signatures
              - Use regex for flexible URL or path matching
              - Test rules thoroughly before enabling in production
            """;

        _sectionContent["22. How to Use: Admin Panel"] = """
            HOW TO USE: ADMIN PANEL
            ══════════════════════════════════════════════

            Accessing the Admin Panel:
              1. Log in with an administrator account
              2. The Admin Panel tab appears in the navigation menu
              3. Non-admin users cannot see or access this tab

            Managing Users:
              1. View all registered users in the user list
              2. To kick a user: Select them and click "Kick" to disconnect
                 their active session (they can reconnect)
              3. To ban a user: Select them and click "Ban" to permanently
                 block their account from connecting
              4. To remove a user: Select them and click "Remove" to delete
                 their account entirely
              5. To change a password: Select a user and enter a new password

            Sending Broadcast Notifications:
              1. Navigate to the broadcast section of the Admin Panel
              2. Type your notification message
              3. Optionally set a countdown timer for time-sensitive alerts
              4. Click "Send" to push the notification to all connected users
              5. The notification appears as a banner on all client applications

            Viewing Server Metrics:
              - Monitor server health including CPU, memory, and disk usage
              - Track connected client count and active sessions
              - Review API request rates and response performance
              - Check service uptime and availability history

            Running Diagnostics:
              - View system information (OS, hardware, memory, uptime)
              - Check application status (LLM model, database, signatures)
              - Review client connection info (local and public IP)
              - Access the diagnostic log for troubleshooting
            """;

        _sectionContent["23. Settings"] = """
            SETTINGS
            ══════════════════════════════════════════════

            Centralized configuration for all SyntheticAI features.

            General:
              - Start with Windows: Launch SyntheticAI on system startup
              - Minimize to Tray: Keep running in the background
              - Notifications: Configure alert preferences

            Scanner:
              - Real-Time Protection: Toggle live file system monitoring
              - Archive Scanning: Scan inside compressed files
              - Exclusions: Specify files or folders to skip
              - Sensitivity: Adjust heuristic detection threshold

            Firewall:
              - Default Action: Allow or block unmatched traffic
              - Logging Level: Control firewall event verbosity

            LLM:
              - Model selection and configuration
              - Temperature: Control response creativity
              - Max Tokens: Limit response length
              - System Prompt: Customize AI personality and behavior

            Security:
              - USB/Registry/Network/Process monitoring toggles
              - AI Detection sensitivity
              - Endpoint protection preferences

            Shutdown App: Use the Shutdown button in Settings to fully close
            the application (otherwise closing the window minimizes to tray).

            Reset Defaults: Restore all settings to factory defaults.
            """;

        _sectionContent["24. Keyboard Shortcuts"] = """
            KEYBOARD SHORTCUTS & TIPS
            ══════════════════════════════════════════════

            Navigation:
              Ctrl + 1-8    - Navigate to different sections
              Ctrl + ,      - Open Settings
              F1            - Open Help & Documentation

            Scanner:
              Ctrl+Shift+Q  - Start Quick Scan
              Ctrl+Shift+F  - Start Full Scan
              Escape        - Cancel current scan

            General:
              Ctrl+Shift+M  - Minimize to system tray
              Ctrl+L        - Clear chat history

            Tips:
              - Run a Full Scan at least weekly for comprehensive coverage
              - Keep Endpoint Protection enabled at all times
              - Enable VPN Kill Switch on public WiFi networks
              - Use the /security chat command for personalized security advice
              - Review firewall rules periodically for stale entries
              - Enable USB monitoring when using external drives
              - Create YARA rules for threats specific to your organization
              - Check the Admin Panel regularly for server health metrics
              - Use Broadcast Chat for team-wide security communications
            """;

        _sectionContent["25. Troubleshooting"] = """
            TROUBLESHOOTING
            ══════════════════════════════════════════════

            App won't start:
              - Ensure .NET 8.0 runtime is installed (Windows Desktop)
              - Ensure .NET 9.0 runtime is installed (Linux CLI)
              - Check no other instance is running (system tray)
              - Run as Administrator for full functionality
              - Check logs in the 'logs' folder for error details

            Scan is slow:
              - Use Quick Scan for routine checks
              - Check for conflicting antivirus software
              - Exclude network drives in Settings > Scanner
              - Large drives with many files will take longer for Full Scans

            Firewall rules not applying:
              - Verify Windows Firewall service is running
              - Run SyntheticAI as Administrator
              - Check for conflicting rules from other security software

            AI not responding:
              - Wait for model loading to complete (progress indicator shows status)
              - Check logs for model load failure messages
              - Ensure the LLM model file is in the correct directory
              - Verify sufficient RAM is available for the model

            VPN connection fails:
              - Verify internet connection is working
              - Ensure WireGuard is properly installed
              - Try a different server from the server list
              - Check if another VPN client is conflicting

            Mobile app issues:
              - Ensure the mobile app has necessary permissions granted
              - Check that the server URL is correctly configured
              - Verify the device has an active internet connection
              - For Bluetooth/WiFi scanning, ensure location permission is granted

            High resource usage:
              - Disable unnecessary monitoring features in Settings
              - Close the Processes view when not actively using it
              - Reduce LLM token limit if AI responses are slow
            """;

        _sectionContent["26. About"] = """
            ABOUT
            ══════════════════════════════════════════════

            SGL SyntheticAI - AI-Powered Security Suite
            Developed by Synthetic Game Labs (SGL)

            Version: BetaV1.1.28
            Platforms:
              - Windows Desktop (WPF / .NET 8)
              - Linux CLI (.NET 9)
              - Android Mobile (.NET MAUI)
              - Web Dashboard (React)
            AI Engine: Local LLM with Thinking UI
            Server API: ASP.NET Core Kestrel
            Authentication: JWT token-based
            WebSocket Gateway: Real-time device communication
            Public Domain: syntheticgamelabs.dpdns.org (Cloudflare tunnel)
            License: Proprietary

            BetaV1.1.28 Features:
              - AI Security Scanner with YARA rule integration
              - AI Chat with thinking UI (expandable thought bubbles)
              - Text-to-speech with 9 voice options
              - Knowledge library for offline learning
              - Quarantine vault with threat actions (quarantine/ignore/watch with AI)
              - Real Windows Firewall management
              - WireGuard-based VPN service with kill switch
              - Real-time network traffic monitoring
              - Endpoint protection with behavioral anomaly detection
              - Git repository hosting with file browsing and downloads
              - Broadcast chat with blackboard messaging and countdown timers
              - YARA rule engine with custom creation, import/export
              - Telemetry detection with force-stop capability
              - QR code sharing for app download links
              - Admin panel with server metrics, user management (kick/ban/remove)
              - Device protection on mobile (camera/mic blocker, permission scanning)
              - Bluetooth scanner on mobile (real discovery, threat analysis)
              - WiFi scanner on mobile (network scanning, connection testing, trace route)
              - Multi-platform support (Windows, Linux, Android, React web)
              - Local LLM integration with chain-of-thought thinking UI
              - JWT-authenticated client-server architecture
              - WebSocket gateway for real-time communication

            Credits:
              - Synthetic Game Labs development team
              - CommunityToolkit.Mvvm (Microsoft Community Toolkit)
              - LLamaSharp (llama.cpp wrapper for .NET)
              - WireGuard (VPN protocol)
              - ASP.NET Core (embedded server API)
              - React (web dashboard)

            Contact & Support:
              - Email: syntheticgamelabs@gmail.com
              - Include application logs when reporting issues

            Stay vigilant. Stay protected.
            """;
    }
}
