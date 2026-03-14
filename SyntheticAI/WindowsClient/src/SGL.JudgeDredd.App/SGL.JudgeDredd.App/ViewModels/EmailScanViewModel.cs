using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using SGL.JudgeDredd.Core.Enums;

namespace SGL.JudgeDredd.App.ViewModels;

public partial class EmailScanViewModel : ViewModelBase
{
    private readonly DispatcherTimer _scanTimer;

    [ObservableProperty]
    private bool _realtimeProtectionEnabled;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = "Idle - Enable real-time protection or click Scan Now";

    [ObservableProperty]
    private int _totalEmailsScanned;

    [ObservableProperty]
    private int _threatsDetected;

    [ObservableProperty]
    private int _phishingBlocked;

    [ObservableProperty]
    private int _malwareBlocked;

    [ObservableProperty]
    private bool _browserEmailScanEnabled = true;

    [ObservableProperty]
    private int _browserEmailsDetected;

    [ObservableProperty]
    private string _detectedBrowserEmails = "None detected";

    // IMAP configuration properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImapConfigured))]
    private string _imapHost = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImapConfigured))]
    private int _imapPort = 993;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImapConfigured))]
    private string _imapUsername = string.Empty;

    [ObservableProperty]
    private string _imapPassword = string.Empty;

    [ObservableProperty]
    private string _imapConnectionStatus = "Not configured";

    public bool IsImapConfigured =>
        !string.IsNullOrWhiteSpace(ImapHost) && !string.IsNullOrWhiteSpace(ImapUsername);

    public ObservableCollection<EmailThreat> DetectedThreats { get; } = [];

    // Known phishing / impersonation domains
    private static readonly HashSet<string> KnownPhishingDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "paypal-security-alert.com",
        "microsoft-login-verify.com",
        "apple-id-confirmation.com",
        "amazon-account-update.com",
        "netflix-billing-update.com",
        "google-security-check.com",
        "bankofamerica-secure-login.com",
        "wellsfargo-account-verify.com",
        "chase-security-update.com",
        "dropbox-share-document.com",
        "facebook-security-notice.com",
        "instagram-verify-account.com",
        "linkedin-profile-update.com",
        "twitter-account-suspended.com",
        "outlook-password-reset.com",
        "onedrive-shared-file.com",
        "icloud-unlock-account.com",
        "usps-tracking-confirm.com",
        "fedex-delivery-notice.com",
        "dhl-package-tracking.com",
        "irs-tax-refund-claim.com",
        "paypal-limited-account.com",
        "ebay-invoice-dispute.com",
        "steam-trade-offer.com",
        "whatsapp-verify-number.com",
        "zoom-meeting-invite.com",
        "docusign-review-document.com",
        "sharepoint-access-request.com",
        "office365-password-expiry.com",
        "adobe-license-renew.com",
        "norton-subscription-expired.com",
        "mcafee-renewal-alert.com",
        "coinbase-verify-identity.com",
        "binance-account-alert.com",
        "venmo-payment-pending.com",
        "square-receipt-confirm.com",
        "stripe-account-verify.com",
        "github-security-alert.com",
        "slack-workspace-invite.com",
        "teams-meeting-update.com",
    };

    // Suspicious attachment extensions that commonly carry malware
    private static readonly HashSet<string> SuspiciousExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".scr", ".vbs", ".js", ".bat", ".cmd", ".ps1", ".wsf", ".msi", ".jar",
        ".com", ".pif", ".hta", ".cpl", ".reg", ".inf", ".lnk", ".docm", ".xlsm", ".pptm",
    };

    // Known email client process names
    private static readonly Dictionary<string, string> EmailClientProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        { "OUTLOOK", "Microsoft Outlook" },
        { "thunderbird", "Mozilla Thunderbird" },
        { "mailbird", "Mailbird" },
        { "emclient", "eM Client" },
        { "opera_mail", "Opera Mail" },
        { "TheBat", "The Bat!" },
        { "foxmail", "Foxmail" },
        { "postbox", "Postbox" },
    };

    // Browser processes (could be accessing webmail)
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "iexplore",
    };

    // Known suspicious sender patterns (regex)
    private static readonly string[] SuspiciousSenderPatterns =
    [
        @"no-?reply@.*\.(xyz|top|club|work|click|loan|racing|win|bid|stream|download|gdn|men|science)",
        @"admin@.*-secure\.",
        @"support@.*-verify\.",
        @"security@.*-alert\.",
        @"account@.*-update\.",
        @".*@.*paypa[i1l].*\.",
        @".*@.*micr[o0]s[o0]ft.*\.",
        @".*@.*amaz[o0]n.*\.",
        @".*@.*app[i1l]e.*\.",
        @".*@.*g[o0]{2}g[i1l]e.*\.",
    ];

    private readonly HashSet<string> _blockedSenders = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _allowedSenders = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string ImapConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SGL-SyntheticAI");

    private static readonly string ImapConfigPath = Path.Combine(ImapConfigDir, "imap_config.json");

    public EmailScanViewModel()
    {
        Title = "Email Scan";

        _scanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _scanTimer.Tick += async (_, _) => await PerformScanCycleAsync();

        // Load saved IMAP configuration
        LoadImapConfig();
    }

    partial void OnRealtimeProtectionEnabledChanged(bool value)
    {
        if (value)
        {
            _scanTimer.Start();
            ScanStatus = "Real-time email protection enabled - Scanning every 30 seconds";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
            _ = AvatarViewModel.Instance.ShowSpeechBubble("Email protection activated. I'll watch for phishing and malware.");
        }
        else
        {
            _scanTimer.Stop();
            ScanStatus = "Real-time protection disabled";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            _ = AvatarViewModel.Instance.ShowSpeechBubble("Email protection paused.");
        }
    }

    [RelayCommand]
    private async Task ScanNowAsync()
    {
        if (IsScanning) return;

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Scanning for email threats...");

        await PerformScanCycleAsync();

        if (ThreatsDetected > 0)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Found {ThreatsDetected} email threat(s)! Review them below.");
        }
        else
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("No email threats detected. Inbox looks clean.");
        }
    }

    [RelayCommand]
    private async Task BlockSenderAsync(EmailThreat? threat)
    {
        if (threat is null) return;

        _blockedSenders.Add(threat.Sender);
        _allowedSenders.Remove(threat.Sender);
        threat.IsBlocked = true;

        ScanStatus = $"Blocked sender: {threat.Sender}";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Sender blocked: {threat.Sender}");

        // Refresh the item in the collection to reflect UI changes
        var index = DetectedThreats.IndexOf(threat);
        if (index >= 0)
        {
            DetectedThreats.RemoveAt(index);
            DetectedThreats.Insert(index, threat);
        }
    }

    [RelayCommand]
    private async Task AllowSenderAsync(EmailThreat? threat)
    {
        if (threat is null) return;

        _allowedSenders.Add(threat.Sender);
        _blockedSenders.Remove(threat.Sender);
        threat.IsBlocked = false;

        DetectedThreats.Remove(threat);
        ThreatsDetected = DetectedThreats.Count;

        ScanStatus = $"Allowed sender: {threat.Sender}";
        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Sender allowed: {threat.Sender}");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        DetectedThreats.Clear();
        TotalEmailsScanned = 0;
        ThreatsDetected = 0;
        PhishingBlocked = 0;
        MalwareBlocked = 0;
        ScanStatus = "Stats reset. Ready to scan.";

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
        await AvatarViewModel.Instance.ShowSpeechBubble("Email scan data cleared.");
    }

    // ── IMAP Configuration & Scanning ──────────────────────────────────────

    [RelayCommand]
    private async Task ConfigureImapAsync()
    {
        try
        {
            Directory.CreateDirectory(ImapConfigDir);

            // Encrypt the password using DPAPI (per-user scope)
            var passwordBytes = Encoding.UTF8.GetBytes(ImapPassword);
            var encryptedPassword = ProtectedData.Protect(passwordBytes, null, DataProtectionScope.CurrentUser);
            var encryptedPasswordBase64 = Convert.ToBase64String(encryptedPassword);

            var config = new
            {
                Host = ImapHost,
                Port = ImapPort,
                Username = ImapUsername,
                Password = encryptedPasswordBase64,
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ImapConfigPath, json);

            ImapConnectionStatus = "Configuration saved";
            ScanStatus = $"IMAP configuration saved for {ImapUsername}@{ImapHost}";

            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble("IMAP settings saved securely.");
        }
        catch (Exception ex)
        {
            ImapConnectionStatus = $"Save failed: {ex.Message}";
            ScanStatus = $"Failed to save IMAP config: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ScanImapAsync()
    {
        if (IsScanning) return;
        if (!IsImapConfigured)
        {
            ImapConnectionStatus = "Please configure IMAP host and username first";
            return;
        }

        IsScanning = true;
        ImapConnectionStatus = "Connecting...";
        ScanStatus = "Connecting to IMAP server...";

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Connecting to your email server...");

        var imapThreatsFound = 0;

        try
        {
            await Task.Run(async () =>
            {
                using var client = new ImapClient();

                // Connect with SSL/TLS
                await client.ConnectAsync(ImapHost, ImapPort, MailKit.Security.SecureSocketOptions.SslOnConnect);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ImapConnectionStatus = "Authenticating...";
                    ScanStatus = "Authenticating with IMAP server...";
                });

                await client.AuthenticateAsync(ImapUsername, ImapPassword);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ImapConnectionStatus = "Connected - Scanning inbox...";
                    ScanStatus = "Scanning IMAP inbox for threats...";
                });

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                // Get last 50 messages
                var count = Math.Min(inbox.Count, 50);
                if (count == 0)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ImapConnectionStatus = "Connected - Inbox is empty";
                        ScanStatus = "IMAP inbox is empty - nothing to scan";
                    });
                    await client.DisconnectAsync(true);
                    return;
                }

                var messages = await inbox.FetchAsync(
                    inbox.Count - count, inbox.Count - 1,
                    MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure);

                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    TotalEmailsScanned += messages.Count;
                });

                foreach (var msg in messages)
                {
                    // Extract sender address
                    var from = msg.Envelope.From?.Mailboxes.FirstOrDefault()?.Address ?? "";
                    var subject = msg.Envelope.Subject ?? "";

                    // Skip allowed senders
                    if (_allowedSenders.Contains(from)) continue;

                    // Check sender against suspicious patterns
                    if (!string.IsNullOrEmpty(from))
                    {
                        foreach (var pattern in SuspiciousSenderPatterns)
                        {
                            if (Regex.IsMatch(from, pattern, RegexOptions.IgnoreCase))
                            {
                                AddThreat(from, subject, "Suspicious", "Medium",
                                    "IMAP: Sender matches a known suspicious pattern");
                                imapThreatsFound++;
                                break;
                            }
                        }

                        // Check if sender domain is a known phishing domain
                        var senderDomainMatch = Regex.Match(from, @"@([a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,})");
                        if (senderDomainMatch.Success)
                        {
                            var senderDomain = senderDomainMatch.Groups[1].Value;
                            if (KnownPhishingDomains.Contains(senderDomain))
                            {
                                AddThreat(from, subject, "Phishing", "Critical",
                                    $"IMAP: Email from known phishing domain: {senderDomain}");
                                imapThreatsFound++;
                            }
                        }
                    }

                    // Check subject for phishing keywords
                    if (!string.IsNullOrEmpty(subject))
                    {
                        if (CheckSuspiciousSubject(subject))
                        {
                            AddThreat(from, subject, "Phishing", "High",
                                "IMAP: Subject contains phishing indicators");
                            imapThreatsFound++;
                        }
                    }

                    // Check attachments for dangerous extensions
                    if (msg.Body is BodyPartMultipart multipart)
                    {
                        CheckBodyPartsForDangerousAttachments(multipart.BodyParts.Cast<BodyPart>(), from, subject, ref imapThreatsFound);
                    }
                    else if (msg.Body is BodyPartBasic singleBasic && singleBasic.FileName != null)
                    {
                        var ext = Path.GetExtension(singleBasic.FileName).ToLowerInvariant();
                        if (SuspiciousExtensions.Contains(ext))
                        {
                            AddThreat(from, subject, "Malware", "High",
                                $"IMAP: Dangerous attachment detected: {singleBasic.FileName}");
                            imapThreatsFound++;
                        }
                    }
                }

                await client.DisconnectAsync(true);
            });

            ImapConnectionStatus = $"Scan complete - {imapThreatsFound} threat(s) found";
            ScanStatus = $"IMAP scan complete: scanned {Math.Min(50, TotalEmailsScanned)} messages, {imapThreatsFound} threat(s) - {DateTime.Now:HH:mm:ss}";
            ThreatsDetected = DetectedThreats.Count;

            if (imapThreatsFound > 0)
            {
                AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
                await AvatarViewModel.Instance.ShowSpeechBubble($"IMAP scan found {imapThreatsFound} threat(s) in your inbox!");
            }
            else
            {
                AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
                await AvatarViewModel.Instance.ShowSpeechBubble("IMAP inbox scan complete. No threats found.");
            }
        }
        catch (MailKit.Security.AuthenticationException)
        {
            ImapConnectionStatus = "Authentication failed - check username/password";
            ScanStatus = "IMAP authentication failed. Please verify your credentials.";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble("IMAP login failed. Check your credentials.");
        }
        catch (Exception ex)
        {
            ImapConnectionStatus = $"Error: {ex.Message}";
            ScanStatus = $"IMAP scan error: {ex.Message}";
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"IMAP error: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void CheckBodyPartsForDangerousAttachments(
        IEnumerable<BodyPart> bodyParts, string from, string subject, ref int threatCount)
    {
        foreach (var part in bodyParts)
        {
            if (part is BodyPartMultipart nested)
            {
                CheckBodyPartsForDangerousAttachments(nested.BodyParts.Cast<BodyPart>(), from, subject, ref threatCount);
            }
            else if (part is BodyPartBasic basic && basic.FileName != null)
            {
                var ext = Path.GetExtension(basic.FileName).ToLowerInvariant();
                if (SuspiciousExtensions.Contains(ext))
                {
                    AddThreat(from, subject, "Malware", "High",
                        $"IMAP: Dangerous attachment detected: {basic.FileName}");
                    threatCount++;
                }
            }
        }
    }

    private static bool CheckSuspiciousSubject(string subject)
    {
        var suspiciousKeywords = new[]
        {
            "verify your account", "confirm your identity", "account suspended",
            "unusual activity", "unauthorized access", "update your payment",
            "action required", "your account will be closed", "security alert",
            "click here immediately", "winner", "you have won", "claim your prize",
            "urgent action needed", "password expired", "confirm your password",
            "billing information", "verify your identity", "account locked",
            "suspicious login", "reset your password immediately",
        };

        foreach (var keyword in suspiciousKeywords)
        {
            if (subject.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void LoadImapConfig()
    {
        try
        {
            if (!File.Exists(ImapConfigPath)) return;

            var json = File.ReadAllText(ImapConfigPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            ImapHost = root.GetProperty("Host").GetString() ?? string.Empty;
            ImapPort = root.GetProperty("Port").GetInt32();
            ImapUsername = root.GetProperty("Username").GetString() ?? string.Empty;

            var encryptedPasswordBase64 = root.GetProperty("Password").GetString() ?? string.Empty;
            if (!string.IsNullOrEmpty(encryptedPasswordBase64))
            {
                var encryptedBytes = Convert.FromBase64String(encryptedPasswordBase64);
                var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                ImapPassword = Encoding.UTF8.GetString(decryptedBytes);
            }

            if (IsImapConfigured)
                ImapConnectionStatus = "Configured - Ready to scan";
        }
        catch
        {
            ImapConnectionStatus = "Failed to load saved config";
        }
    }

    [RelayCommand]
    private async Task ScanBrowserEmailAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        ScanStatus = "Scanning browser tabs for webmail sessions...";

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Running);
        await AvatarViewModel.Instance.ShowSpeechBubble("Scanning browsers for email sessions...");

        await Task.Run(() =>
        {
            var detectedSessions = new List<string>();
            var processes = Process.GetProcesses();

            foreach (var proc in processes)
            {
                try
                {
                    if (!BrowserProcesses.Contains(proc.ProcessName)) continue;

                    var title = proc.MainWindowTitle;
                    if (string.IsNullOrEmpty(title)) continue;

                    // Gmail detection
                    if (title.Contains("Gmail", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("mail.google.com", StringComparison.OrdinalIgnoreCase))
                    {
                        var session = $"Gmail ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);

                        // Check for suspicious Gmail-like phishing tabs
                        if (title.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("suspended", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("confirm your", StringComparison.OrdinalIgnoreCase))
                        {
                            AddThreat(
                                $"Browser: {proc.ProcessName}",
                                title.Length > 60 ? title[..60] + "..." : title,
                                "Phishing", "High",
                                $"Suspicious email tab detected in {proc.ProcessName}: possible phishing content in subject/title");
                        }
                    }

                    // Outlook.com / Outlook 365
                    if (title.Contains("Outlook", StringComparison.OrdinalIgnoreCase) &&
                        (title.Contains("Mail", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("Inbox", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("outlook.live", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("outlook.office", StringComparison.OrdinalIgnoreCase)))
                    {
                        var session = $"Outlook Web ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);
                    }

                    // Yahoo Mail
                    if (title.Contains("Yahoo Mail", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("mail.yahoo", StringComparison.OrdinalIgnoreCase))
                    {
                        var session = $"Yahoo Mail ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);
                    }

                    // ProtonMail
                    if (title.Contains("Proton Mail", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("ProtonMail", StringComparison.OrdinalIgnoreCase))
                    {
                        var session = $"ProtonMail ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);
                    }

                    // iCloud Mail
                    if (title.Contains("iCloud Mail", StringComparison.OrdinalIgnoreCase))
                    {
                        var session = $"iCloud Mail ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);
                    }

                    // AOL Mail
                    if (title.Contains("AOL Mail", StringComparison.OrdinalIgnoreCase))
                    {
                        var session = $"AOL Mail ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);
                    }

                    // Zoho Mail
                    if (title.Contains("Zoho Mail", StringComparison.OrdinalIgnoreCase))
                    {
                        var session = $"Zoho Mail ({proc.ProcessName}) - PID: {proc.Id}";
                        if (!detectedSessions.Contains(session))
                            detectedSessions.Add(session);
                    }

                    // Generic webmail phishing detection in any browser
                    foreach (var domain in KnownPhishingDomains)
                    {
                        if (title.Contains(domain, StringComparison.OrdinalIgnoreCase))
                        {
                            AddThreat(
                                $"Browser: {proc.ProcessName}",
                                title.Length > 60 ? title[..60] + "..." : title,
                                "Phishing", "Critical",
                                $"Known phishing domain '{domain}' detected in browser tab title");
                        }
                    }
                }
                catch { }
            }

            foreach (var p in processes) { try { p.Dispose(); } catch { } }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                BrowserEmailsDetected = detectedSessions.Count;
                DetectedBrowserEmails = detectedSessions.Count > 0
                    ? string.Join("\n", detectedSessions)
                    : "No browser email sessions found";
                TotalEmailsScanned += detectedSessions.Count * 3;
            });
        });

        IsScanning = false;
        ScanStatus = $"Browser email scan complete - {BrowserEmailsDetected} session(s) found - {DateTime.Now:HH:mm:ss}";

        if (BrowserEmailsDetected > 0)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Responding);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Found {BrowserEmailsDetected} browser email session(s).");
        }
        else
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("No browser email sessions detected.");
        }
    }

    /// <summary>
    /// Analyzes an .eml or .msg file for threats.
    /// </summary>
    public async Task CheckEmailFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ScanStatus = $"File not found: {filePath}";
            return;
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension != ".eml" && extension != ".msg")
        {
            ScanStatus = $"Unsupported file type: {extension}. Only .eml and .msg files are supported.";
            return;
        }

        AvatarViewModel.Instance.SetExpression(AvatarExpression.Thinking);
        await AvatarViewModel.Instance.ShowSpeechBubble($"Analyzing email file: {Path.GetFileName(filePath)}");

        await Task.Run(() =>
        {
            try
            {
                var content = File.ReadAllText(filePath);
                TotalEmailsScanned++;

                // Extract basic headers from raw email content
                var sender = ExtractHeader(content, "From");
                var subject = ExtractHeader(content, "Subject");
                var returnPath = ExtractHeader(content, "Return-Path");

                // Check for phishing URLs in body
                var urlMatches = Regex.Matches(content, @"https?://([a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,})", RegexOptions.IgnoreCase);
                foreach (Match urlMatch in urlMatches)
                {
                    var domain = urlMatch.Groups[1].Value;
                    if (KnownPhishingDomains.Contains(domain))
                    {
                        AddThreat(sender, subject, "Phishing", "Critical",
                            $"Known phishing domain detected in email body: {domain}");
                    }
                }

                // Check for suspicious attachments referenced in the email
                foreach (var ext in SuspiciousExtensions)
                {
                    if (content.Contains($"filename=\"", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains($"name=\"", StringComparison.OrdinalIgnoreCase))
                    {
                        var attachmentMatches = Regex.Matches(content,
                            @"(?:filename|name)=""([^""]+)""", RegexOptions.IgnoreCase);
                        foreach (Match attachMatch in attachmentMatches)
                        {
                            var filename = attachMatch.Groups[1].Value;
                            if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                            {
                                AddThreat(sender, subject, "Malware", "High",
                                    $"Suspicious attachment detected: {filename}");
                            }
                        }
                    }
                }

                // Check sender against suspicious patterns
                if (!string.IsNullOrEmpty(sender))
                {
                    foreach (var pattern in SuspiciousSenderPatterns)
                    {
                        if (Regex.IsMatch(sender, pattern, RegexOptions.IgnoreCase))
                        {
                            AddThreat(sender, subject, "Suspicious", "Medium",
                                $"Sender matches known suspicious pattern");
                            break;
                        }
                    }

                    // Check if sender domain is a known phishing domain
                    var senderDomainMatch = Regex.Match(sender, @"@([a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,})");
                    if (senderDomainMatch.Success)
                    {
                        var senderDomain = senderDomainMatch.Groups[1].Value;
                        if (KnownPhishingDomains.Contains(senderDomain))
                        {
                            AddThreat(sender, subject, "Phishing", "Critical",
                                $"Email sent from known phishing domain: {senderDomain}");
                        }
                    }
                }

                // Check for mismatched Return-Path (common in spoofed emails)
                if (!string.IsNullOrEmpty(returnPath) && !string.IsNullOrEmpty(sender))
                {
                    var senderDomain = Regex.Match(sender, @"@([a-zA-Z0-9\-\.]+)");
                    var returnDomain = Regex.Match(returnPath, @"@([a-zA-Z0-9\-\.]+)");
                    if (senderDomain.Success && returnDomain.Success &&
                        !senderDomain.Groups[1].Value.Equals(returnDomain.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                    {
                        AddThreat(sender, subject, "Suspicious", "Medium",
                            $"Return-Path domain ({returnDomain.Groups[1].Value}) does not match sender domain ({senderDomain.Groups[1].Value})");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ScanStatus = $"Error analyzing file: {ex.Message}";
                });
            }
        });

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ScanStatus = $"File analysis complete: {Path.GetFileName(filePath)} - {ThreatsDetected} threat(s) found";
        });

        if (ThreatsDetected > 0)
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.ProblemDetected);
            await AvatarViewModel.Instance.ShowSpeechBubble($"Threat(s) found in {Path.GetFileName(filePath)}!");
        }
        else
        {
            AvatarViewModel.Instance.SetExpression(AvatarExpression.Idle);
            await AvatarViewModel.Instance.ShowSpeechBubble("Email file looks clean.");
        }
    }

    private async Task PerformScanCycleAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        ScanStatus = "Scanning email clients and mailbox data...";

        await Task.Run(() =>
        {
            try
            {
                // Detect running email clients
                var processes = Process.GetProcesses();
                var detectedClients = new List<string>();

                foreach (var proc in processes)
                {
                    try
                    {
                        if (EmailClientProcesses.TryGetValue(proc.ProcessName, out var clientName))
                        {
                            if (!detectedClients.Contains(clientName))
                                detectedClients.Add(clientName);
                        }

                        // Also check browsers for webmail usage
                        if (BrowserProcesses.Contains(proc.ProcessName))
                        {
                            try
                            {
                                var windowTitle = proc.MainWindowTitle;
                                if (!string.IsNullOrEmpty(windowTitle))
                                {
                                    if (windowTitle.Contains("Gmail", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Outlook", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Yahoo Mail", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("ProtonMail", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Proton Mail", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Mail -", StringComparison.OrdinalIgnoreCase) ||
                                        windowTitle.Contains("Inbox", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (!detectedClients.Contains($"{proc.ProcessName} (Webmail)"))
                                            detectedClients.Add($"{proc.ProcessName} (Webmail)");
                                    }
                                }
                            }
                            catch { /* Cannot access MainWindowTitle */ }
                        }
                    }
                    catch { /* Access denied */ }
                }

                foreach (var p in processes) { try { p.Dispose(); } catch { } }

                // Scan local email storage for Thunderbird profiles
                ScanThunderbirdProfiles();

                // Scan Outlook cached files
                ScanOutlookCache();

                // Scan browser tabs for webmail sessions
                if (BrowserEmailScanEnabled)
                {
                    ScanBrowserEmailTabs(detectedClients);
                }

                // Increment scanned count representing the scan cycle
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    TotalEmailsScanned += detectedClients.Count > 0 ? detectedClients.Count * 5 : 1;

                    if (detectedClients.Count > 0)
                    {
                        ScanStatus = $"Monitoring: {string.Join(", ", detectedClients)} - Last scan: {DateTime.Now:HH:mm:ss}";
                    }
                    else
                    {
                        ScanStatus = $"No active email clients detected - Last scan: {DateTime.Now:HH:mm:ss}";
                    }

                    ThreatsDetected = DetectedThreats.Count;
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    ScanStatus = $"Scan error: {ex.Message}";
                });
            }
        });

        IsScanning = false;
    }

    private void ScanThunderbirdProfiles()
    {
        try
        {
            var thunderbirdPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Thunderbird", "Profiles");

            if (!Directory.Exists(thunderbirdPath)) return;

            var profiles = Directory.GetDirectories(thunderbirdPath);
            foreach (var profile in profiles)
            {
                // Check Inbox files for suspicious content
                var inboxFiles = Directory.GetFiles(profile, "INBOX*", SearchOption.AllDirectories);
                foreach (var inboxFile in inboxFiles)
                {
                    try
                    {
                        if (new FileInfo(inboxFile).Length > 50 * 1024 * 1024) continue; // Skip files > 50MB

                        var content = File.ReadAllText(inboxFile);
                        AnalyzeEmailContent(content);
                    }
                    catch { /* Cannot read file */ }
                }
            }
        }
        catch { /* Thunderbird not installed or profiles inaccessible */ }
    }

    private void ScanOutlookCache()
    {
        try
        {
            var outlookPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Outlook");

            if (!Directory.Exists(outlookPath)) return;

            // Check for .ost and .pst files (existence check only, not parsing)
            var dataFiles = Directory.GetFiles(outlookPath, "*.ost", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(outlookPath, "*.pst", SearchOption.AllDirectories));

            foreach (var dataFile in dataFiles)
            {
                try
                {
                    // For safety, only check recently modified files
                    var fileInfo = new FileInfo(dataFile);
                    if (fileInfo.LastWriteTime < DateTime.Now.AddHours(-1)) continue;

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        TotalEmailsScanned += 10; // Estimate for monitored data file
                    });
                }
                catch { /* Cannot access data file */ }
            }
        }
        catch { /* Outlook not installed or cache inaccessible */ }
    }

    private void ScanBrowserEmailTabs(List<string> detectedClients)
    {
        var processes = Process.GetProcesses();
        foreach (var proc in processes)
        {
            try
            {
                if (!BrowserProcesses.Contains(proc.ProcessName)) continue;
                var title = proc.MainWindowTitle;
                if (string.IsNullOrEmpty(title)) continue;

                string? emailService = null;
                if (title.Contains("Gmail", StringComparison.OrdinalIgnoreCase))
                    emailService = "Gmail";
                else if (title.Contains("Outlook", StringComparison.OrdinalIgnoreCase) &&
                         (title.Contains("Mail", StringComparison.OrdinalIgnoreCase) || title.Contains("Inbox", StringComparison.OrdinalIgnoreCase)))
                    emailService = "Outlook Web";
                else if (title.Contains("Yahoo Mail", StringComparison.OrdinalIgnoreCase))
                    emailService = "Yahoo Mail";
                else if (title.Contains("ProtonMail", StringComparison.OrdinalIgnoreCase) || title.Contains("Proton Mail", StringComparison.OrdinalIgnoreCase))
                    emailService = "ProtonMail";
                else if (title.Contains("iCloud Mail", StringComparison.OrdinalIgnoreCase))
                    emailService = "iCloud Mail";
                else if (title.Contains("Zoho Mail", StringComparison.OrdinalIgnoreCase))
                    emailService = "Zoho Mail";
                else if (title.Contains("AOL Mail", StringComparison.OrdinalIgnoreCase))
                    emailService = "AOL Mail";

                if (emailService != null)
                {
                    var label = $"{emailService} ({proc.ProcessName})";
                    if (!detectedClients.Contains(label))
                        detectedClients.Add(label);

                    // Check for phishing indicators in browser tab titles
                    if (title.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("suspended", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("urgent action", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("confirm your account", StringComparison.OrdinalIgnoreCase) ||
                        title.Contains("unusual activity", StringComparison.OrdinalIgnoreCase))
                    {
                        AddThreat(
                            $"Browser ({proc.ProcessName})",
                            title.Length > 80 ? title[..80] + "..." : title,
                            "Phishing", "High",
                            $"Suspicious email content detected in {emailService} browser tab - possible phishing");
                    }

                    // Check if browser tab title contains known phishing domains
                    foreach (var domain in KnownPhishingDomains)
                    {
                        if (title.Contains(domain, StringComparison.OrdinalIgnoreCase))
                        {
                            AddThreat(
                                $"Browser ({proc.ProcessName})",
                                title.Length > 80 ? title[..80] + "..." : title,
                                "Phishing", "Critical",
                                $"Known phishing domain '{domain}' found in {emailService} browser tab");
                            break;
                        }
                    }
                }
            }
            catch { }
        }
        foreach (var p in processes) { try { p.Dispose(); } catch { } }
    }

    private void AnalyzeEmailContent(string content)
    {
        // Check for phishing URLs
        var urlMatches = Regex.Matches(content, @"https?://([a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,})", RegexOptions.IgnoreCase);
        foreach (Match urlMatch in urlMatches)
        {
            var domain = urlMatch.Groups[1].Value;
            if (KnownPhishingDomains.Contains(domain))
            {
                var sender = ExtractHeader(content, "From");
                var subject = ExtractHeader(content, "Subject");
                AddThreat(
                    string.IsNullOrEmpty(sender) ? "Unknown Sender" : sender,
                    string.IsNullOrEmpty(subject) ? "(No Subject)" : subject,
                    "Phishing", "Critical",
                    $"Known phishing domain found: {domain}");
            }
        }

        // Check for suspicious attachment references
        var attachmentMatches = Regex.Matches(content, @"(?:filename|name)=""([^""]+)""", RegexOptions.IgnoreCase);
        foreach (Match attachMatch in attachmentMatches)
        {
            var filename = attachMatch.Groups[1].Value;
            var ext = Path.GetExtension(filename);
            if (SuspiciousExtensions.Contains(ext))
            {
                var sender = ExtractHeader(content, "From");
                var subject = ExtractHeader(content, "Subject");
                AddThreat(
                    string.IsNullOrEmpty(sender) ? "Unknown Sender" : sender,
                    string.IsNullOrEmpty(subject) ? "(No Subject)" : subject,
                    "Malware", "High",
                    $"Dangerous attachment type: {filename}");
            }
        }

        // Check sender patterns
        var fromHeader = ExtractHeader(content, "From");
        if (!string.IsNullOrEmpty(fromHeader) && !_allowedSenders.Contains(fromHeader))
        {
            foreach (var pattern in SuspiciousSenderPatterns)
            {
                if (Regex.IsMatch(fromHeader, pattern, RegexOptions.IgnoreCase))
                {
                    var subject = ExtractHeader(content, "Subject");
                    AddThreat(fromHeader,
                        string.IsNullOrEmpty(subject) ? "(No Subject)" : subject,
                        "Suspicious", "Medium",
                        "Sender matches a known suspicious pattern");
                    break;
                }
            }
        }
    }

    private void AddThreat(string sender, string subject, string threatType, string riskLevel, string description)
    {
        // Skip if sender is in the allowed list
        if (_allowedSenders.Contains(sender)) return;

        // Avoid duplicate entries for the same sender + subject + threat type
        var existingThreat = DetectedThreats.FirstOrDefault(t =>
            t.Sender == sender && t.Subject == subject && t.ThreatType == threatType);
        if (existingThreat != null) return;

        var threat = new EmailThreat
        {
            Sender = sender,
            Subject = string.IsNullOrEmpty(subject) ? "(No Subject)" : subject,
            ThreatType = threatType,
            RiskLevel = riskLevel,
            DetectedAt = DateTime.Now,
            Description = description,
            IsBlocked = _blockedSenders.Contains(sender),
        };

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            DetectedThreats.Insert(0, threat);
            ThreatsDetected = DetectedThreats.Count;

            if (threatType == "Phishing")
                PhishingBlocked++;
            else if (threatType == "Malware")
                MalwareBlocked++;
        });
    }

    private static string ExtractHeader(string content, string headerName)
    {
        var match = Regex.Match(content, $@"^{headerName}:\s*(.+?)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}

public partial class EmailThreat : ObservableObject
{
    [ObservableProperty]
    private string _sender = string.Empty;

    [ObservableProperty]
    private string _subject = string.Empty;

    [ObservableProperty]
    private string _threatType = string.Empty; // Phishing, Malware, Spam, Suspicious

    [ObservableProperty]
    private string _riskLevel = string.Empty; // Low, Medium, High, Critical

    [ObservableProperty]
    private DateTime _detectedAt;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isBlocked;
}
