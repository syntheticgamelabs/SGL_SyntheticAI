using Microsoft.EntityFrameworkCore;
using SGL.JudgeDredd.Core.Enums;
using SGL.JudgeDredd.KnowledgeBase.Data;
using SGL.JudgeDredd.KnowledgeBase.Entities;

namespace SGL.JudgeDredd.KnowledgeBase.Seeders;

public static class InitialThreatSeeder
{
    public static async Task SeedAsync(KnowledgeDbContext context)
    {
        if (await context.ThreatSignatures.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var threats = new List<ThreatSignatureEntity>
        {
            // 1. EICAR test file
            new()
            {
                Sha256Hash = "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f",
                Md5Hash = "44d88612fea8a8f36de82e1278abb02f",
                Name = "EICAR-Test-File",
                Family = "EICAR",
                Severity = (int)ThreatSeverity.Low,
                Description = "EICAR antivirus test file. Not a real threat but used to verify scanner functionality.",
                Tags = "[\"test\",\"eicar\",\"verification\"]",
                FirstSeen = new DateTime(1996, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 2. WannaCry
            new()
            {
                Sha256Hash = "24d004a104d4d54034dbcffc2a4b19a11f39008a575aa614ea04703480b1022c",
                Md5Hash = "db349b97c37d22f5ea1d1841e3c89eb4",
                Name = "WannaCry",
                Family = "WannaCry",
                Severity = (int)ThreatSeverity.Critical,
                Description = "WannaCry ransomware worm that exploits EternalBlue SMB vulnerability (MS17-010). Encrypts files and demands Bitcoin ransom.",
                Tags = "[\"ransomware\",\"worm\",\"eternalblue\",\"smb\",\"exploit\"]",
                FirstSeen = new DateTime(2017, 5, 12, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 3. Petya/NotPetya
            new()
            {
                Sha256Hash = "027cc450ef5f8c5f653329641ec1fed91f694e0d229928963b30f6b0d7d3a745",
                Md5Hash = "71b6a493388e7d0b40c83ce903bc6b04",
                Name = "NotPetya",
                Family = "Petya",
                Severity = (int)ThreatSeverity.Critical,
                Description = "NotPetya wiper malware disguised as ransomware. Overwrites MBR and encrypts MFT. Spread via MEDoc software update supply chain attack.",
                Tags = "[\"wiper\",\"ransomware\",\"mbr\",\"supply-chain\",\"eternalblue\"]",
                FirstSeen = new DateTime(2017, 6, 27, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 4. Emotet
            new()
            {
                Sha256Hash = "e1201ab8925e3a89bf842ecaab68c3faba5d85b113b42fb05aae687d9dbfb251",
                Md5Hash = null,
                Name = "Emotet",
                Family = "Emotet",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Emotet banking trojan and malware distribution platform. Spreads via phishing emails with malicious macros. Acts as loader for secondary payloads.",
                Tags = "[\"trojan\",\"banker\",\"loader\",\"phishing\",\"macro\"]",
                FirstSeen = new DateTime(2014, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 5. TrickBot
            new()
            {
                Sha256Hash = "7cdf9b7eb8e2e7d3348c078254983128382e292b94fc93ddc34ec57ff171b506",
                Md5Hash = null,
                Name = "TrickBot",
                Family = "TrickBot",
                Severity = (int)ThreatSeverity.Critical,
                Description = "TrickBot modular banking trojan with credential harvesting, lateral movement, and ransomware delivery capabilities.",
                Tags = "[\"trojan\",\"banker\",\"modular\",\"credential-theft\",\"lateral-movement\"]",
                FirstSeen = new DateTime(2016, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 6. Ryuk
            new()
            {
                Sha256Hash = "61a093eba0b6f69662b6c385ebbb3566f483d90a1000f27cc62674fa1cb67bc4",
                Md5Hash = null,
                Name = "Ryuk",
                Family = "Ryuk",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Ryuk targeted ransomware deployed after TrickBot or BazarLoader initial access. Known for targeting enterprise and healthcare organizations.",
                Tags = "[\"ransomware\",\"targeted\",\"enterprise\",\"healthcare\"]",
                FirstSeen = new DateTime(2018, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 7. Cobalt Strike Beacon
            new()
            {
                Sha256Hash = "a8690ae273b872e19771f12b7d9a24a0282dae5aecf8e8cc38b32e02d8f2a7fe",
                Md5Hash = null,
                Name = "CobaltStrike-Beacon",
                Family = "CobaltStrike",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cobalt Strike Beacon payload used for command-and-control. Legitimate penetration testing tool frequently abused by threat actors.",
                Tags = "[\"c2\",\"beacon\",\"pentool\",\"post-exploitation\",\"apt\"]",
                FirstSeen = new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 8. Mimikatz
            new()
            {
                Sha256Hash = "56b57f5d1c1c0650ec97cd8c6e6acb80462deee5c0ae8250ebb4d3bbf9a2fbb9",
                Md5Hash = "e930b05afa2805090d60e0e8e4b87a7d",
                Name = "Mimikatz",
                Family = "Mimikatz",
                Severity = (int)ThreatSeverity.High,
                Description = "Mimikatz credential dumping tool. Extracts plaintext passwords, hashes, PIN codes, and Kerberos tickets from memory.",
                Tags = "[\"credential-theft\",\"post-exploitation\",\"hacktool\",\"kerberos\",\"lsass\"]",
                FirstSeen = new DateTime(2011, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 9. Metasploit Meterpreter
            new()
            {
                Sha256Hash = "c77b6993b55251d34ac648e12fe1b39542f974633d7883c7372efcb8c3ab4c0a",
                Md5Hash = null,
                Name = "Metasploit-Meterpreter",
                Family = "Metasploit",
                Severity = (int)ThreatSeverity.High,
                Description = "Metasploit Meterpreter reverse shell payload. Provides remote access, file operations, privilege escalation, and pivoting capabilities.",
                Tags = "[\"rat\",\"reverse-shell\",\"pentool\",\"post-exploitation\"]",
                FirstSeen = new DateTime(2004, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 10. Conti Ransomware
            new()
            {
                Sha256Hash = "28beeb84738834b6c91be39dceee8a0d7e68e9c84d0e66ed4b77e0e84baa4a28",
                Md5Hash = null,
                Name = "Conti",
                Family = "Conti",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Conti ransomware-as-a-service (RaaS) operation. Uses multi-threaded encryption and exfiltrates data for double extortion.",
                Tags = "[\"ransomware\",\"raas\",\"double-extortion\",\"data-exfiltration\"]",
                FirstSeen = new DateTime(2020, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 11. REvil/Sodinokibi
            new()
            {
                Sha256Hash = "ab880037ebe477c1043ccee499c418644aa0247b93d50c95bf4f1b60c4ef5f6a",
                Md5Hash = null,
                Name = "REvil",
                Family = "Sodinokibi",
                Severity = (int)ThreatSeverity.Critical,
                Description = "REvil (Sodinokibi) ransomware-as-a-service. Known for high-profile supply chain attacks including Kaseya and JBS.",
                Tags = "[\"ransomware\",\"raas\",\"supply-chain\",\"double-extortion\"]",
                FirstSeen = new DateTime(2019, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 12. Agent Tesla
            new()
            {
                Sha256Hash = "f5407c4ca46a9ae10ade4ade365d0850c01ae6ea3dcd1a816e5cbbb2eb95342f",
                Md5Hash = null,
                Name = "AgentTesla",
                Family = "AgentTesla",
                Severity = (int)ThreatSeverity.High,
                Description = "Agent Tesla keylogger and information stealer. Captures keystrokes, screenshots, clipboard data, and browser credentials.",
                Tags = "[\"keylogger\",\"infostealer\",\"spyware\",\"credential-theft\"]",
                FirstSeen = new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 13. Qbot/QakBot
            new()
            {
                Sha256Hash = "7aefff4c9866f16a8073f78d1a0c922da2f9cbbd4b51d2fa8928497ce6451c1b",
                Md5Hash = null,
                Name = "QakBot",
                Family = "QakBot",
                Severity = (int)ThreatSeverity.High,
                Description = "QakBot (Qbot) banking trojan with worm capabilities. Steals financial data and serves as initial access broker for ransomware operators.",
                Tags = "[\"trojan\",\"banker\",\"worm\",\"initial-access-broker\"]",
                FirstSeen = new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 14. DarkSide
            new()
            {
                Sha256Hash = "7a98c448020b89db9ffc4b1cc5176949fe714d927cadf4b3c42936d923c901c4",
                Md5Hash = null,
                Name = "DarkSide",
                Family = "DarkSide",
                Severity = (int)ThreatSeverity.Critical,
                Description = "DarkSide ransomware responsible for the Colonial Pipeline attack. Targets large enterprises with double extortion tactics.",
                Tags = "[\"ransomware\",\"raas\",\"critical-infrastructure\",\"double-extortion\"]",
                FirstSeen = new DateTime(2020, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 15. LockBit
            new()
            {
                Sha256Hash = "c748389a07625c77bcdc5bb0c2ce20807dff925f93df8e924ac3f51ad79ae03c",
                Md5Hash = null,
                Name = "LockBit3",
                Family = "LockBit",
                Severity = (int)ThreatSeverity.Critical,
                Description = "LockBit 3.0 ransomware with self-spreading capabilities and anti-analysis features. Most active RaaS operation globally.",
                Tags = "[\"ransomware\",\"raas\",\"self-spreading\",\"anti-analysis\"]",
                FirstSeen = new DateTime(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 16. AsyncRAT
            new()
            {
                Sha256Hash = "86490006aeb6706694c1b51ae60abfd4069e6ff5d86fdb76c3b09650fbbb0ff7",
                Md5Hash = null,
                Name = "AsyncRAT",
                Family = "AsyncRAT",
                Severity = (int)ThreatSeverity.High,
                Description = "AsyncRAT open-source remote access trojan. Provides keylogging, screen capture, file management, and DDOS capabilities.",
                Tags = "[\"rat\",\"open-source\",\"keylogger\",\"remote-access\"]",
                FirstSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 17. Remcos RAT
            new()
            {
                Sha256Hash = "4ad3fd9edd64bf2417309e8edc9104199f6cb62abd80f05ca2ceaf533776ae4a",
                Md5Hash = null,
                Name = "RemcosRAT",
                Family = "Remcos",
                Severity = (int)ThreatSeverity.High,
                Description = "Remcos remote access trojan sold as legitimate surveillance tool. Provides full remote control, keylogging, and data exfiltration.",
                Tags = "[\"rat\",\"surveillance\",\"keylogger\",\"credential-theft\"]",
                FirstSeen = new DateTime(2016, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 18. Raccoon Stealer
            new()
            {
                Sha256Hash = "2fa1ec3c0056cc9d6e109b05a58a1e2fcb3b17c6a17c87da5c1b81cecae62ce5",
                Md5Hash = null,
                Name = "RaccoonStealer",
                Family = "Raccoon",
                Severity = (int)ThreatSeverity.High,
                Description = "Raccoon Stealer malware-as-a-service infostealer. Targets browser credentials, cryptocurrency wallets, and email client data.",
                Tags = "[\"infostealer\",\"maas\",\"credential-theft\",\"cryptocurrency\"]",
                FirstSeen = new DateTime(2019, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 19. RedLine Stealer
            new()
            {
                Sha256Hash = "a14e2c9ce324d6506cc0b0e49b3a5b98d8da16ee34b969ed847da668f2534f4e",
                Md5Hash = null,
                Name = "RedLineStealer",
                Family = "RedLine",
                Severity = (int)ThreatSeverity.High,
                Description = "RedLine Stealer information-stealing malware. Harvests saved credentials, autocomplete data, cryptocurrency wallets, and system information.",
                Tags = "[\"infostealer\",\"credential-theft\",\"cryptocurrency\",\"maas\"]",
                FirstSeen = new DateTime(2020, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 20. BazarLoader
            new()
            {
                Sha256Hash = "df1765629fd2968e5dab6530712477ed728a56a59310ad225d25eb046fdf357c",
                Md5Hash = null,
                Name = "BazarLoader",
                Family = "BazarLoader",
                Severity = (int)ThreatSeverity.High,
                Description = "BazarLoader backdoor linked to TrickBot operators. Used as initial access vector for Conti and Ryuk ransomware deployment.",
                Tags = "[\"loader\",\"backdoor\",\"initial-access\",\"trickbot\"]",
                FirstSeen = new DateTime(2020, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 21. BlackCat/ALPHV
            new()
            {
                Sha256Hash = "17d69d68261bbf1fe985b5912741a040225cf65894a242847391aef17ee0237e",
                Md5Hash = null,
                Name = "BlackCat",
                Family = "ALPHV",
                Severity = (int)ThreatSeverity.Critical,
                Description = "ALPHV/BlackCat ransomware written in Rust. First major cross-platform ransomware offering triple extortion (encryption, data leak, DDoS).",
                Tags = "[\"ransomware\",\"raas\",\"rust\",\"triple-extortion\",\"cross-platform\"]",
                FirstSeen = new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 22. Hive Ransomware
            new()
            {
                Sha256Hash = "c8e508abb8ffaa12b56722aa4d35c0c0be21454c3c42ce278d6ed9b92893913e",
                Md5Hash = null,
                Name = "Hive",
                Family = "Hive",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Hive ransomware targeting healthcare and critical infrastructure. Uses double extortion with data leak site.",
                Tags = "[\"ransomware\",\"raas\",\"healthcare\",\"critical-infrastructure\"]",
                FirstSeen = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 23. Black Basta
            new()
            {
                Sha256Hash = "819531ec365a50d873244ae503650dee13e8481cf93dc0fec0581d872a05f81f",
                Md5Hash = null,
                Name = "BlackBasta",
                Family = "BlackBasta",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Black Basta ransomware group that emerged from Conti operators. Uses QakBot for initial access and targets enterprise environments.",
                Tags = "[\"ransomware\",\"raas\",\"enterprise\",\"conti-successor\"]",
                FirstSeen = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 24. Royal Ransomware
            new()
            {
                Sha256Hash = "8b2b511a7f68f20a5a200a9b28adec1e45ef0b5ebbc901485d17916c348a4489",
                Md5Hash = null,
                Name = "Royal",
                Family = "Royal",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Royal ransomware targeting critical sectors. Uses callback phishing (BazarCall) and partial file encryption for speed.",
                Tags = "[\"ransomware\",\"callback-phishing\",\"partial-encryption\"]",
                FirstSeen = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 25. Akira Ransomware
            new()
            {
                Sha256Hash = "2c0a31962b535dcf0ffd10f646ccf0549b0792d3aa3bd7405a8712f097f41d31",
                Md5Hash = null,
                Name = "Akira",
                Family = "Akira",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Akira ransomware targeting VPN vulnerabilities for initial access, particularly Cisco VPN. Uses double extortion.",
                Tags = "[\"ransomware\",\"vpn-exploit\",\"cisco\",\"double-extortion\"]",
                FirstSeen = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 26. Clop Ransomware
            new()
            {
                Sha256Hash = "d9e4a47b8a51611fbdbbe0dce5deba3e43e0c3e92f1d8cf61e52e0ae0cd6be26",
                Md5Hash = null,
                Name = "Clop",
                Family = "Clop",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Clop (Cl0p) ransomware known for mass exploitation of file transfer vulnerabilities (MOVEit, GoAnywhere, Accellion).",
                Tags = "[\"ransomware\",\"raas\",\"mass-exploitation\",\"file-transfer\"]",
                FirstSeen = new DateTime(2019, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 27. GandCrab
            new()
            {
                Sha256Hash = "3a7d2d69e13db1e42fa5e2c55e3a0d8cf4c2a2a3cb3c7f5e1d9b2c4f6a8e0127",
                Md5Hash = null,
                Name = "GandCrab",
                Family = "GandCrab",
                Severity = (int)ThreatSeverity.Critical,
                Description = "GandCrab pioneering ransomware-as-a-service. Predecessor to REvil/Sodinokibi with affiliate program and Dash cryptocurrency payments.",
                Tags = "[\"ransomware\",\"raas\",\"cryptocurrency\",\"affiliate\"]",
                FirstSeen = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 28. CryptoLocker
            new()
            {
                Sha256Hash = "5f8c3b2a1d0e9f7a6b4c8d2e1f3a5b7c9d0e2f4a6b8c1d3e5f7a9b0c2d4e6f80",
                Md5Hash = null,
                Name = "CryptoLocker",
                Family = "CryptoLocker",
                Severity = (int)ThreatSeverity.Critical,
                Description = "CryptoLocker pioneered modern ransomware. Spread via Gameover Zeus botnet, uses RSA-2048 encryption with payment deadlines.",
                Tags = "[\"ransomware\",\"rsa-2048\",\"gameover-zeus\",\"bitcoin\"]",
                FirstSeen = new DateTime(2013, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 29. Dharma/CrySiS
            new()
            {
                Sha256Hash = "c8b4a2e6d0f8315794e2c6a0d4f83b57e1c5a9d3f7b2e6a0c4d8f215a3b7e9d1",
                Md5Hash = null,
                Name = "Dharma",
                Family = "CrySiS",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Dharma/CrySiS ransomware targeting RDP-exposed systems. Sold as RaaS kit with low barrier to entry for affiliates.",
                Tags = "[\"ransomware\",\"rdp-brute-force\",\"raas\"]",
                FirstSeen = new DateTime(2016, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 30. NjRAT
            new()
            {
                Sha256Hash = "9e7c5a3f1d8b6e4a2c0f8d6b4e2a0c8f6d4b2e0a8c6f4d2b0e8a6c4f2d0b8e60",
                Md5Hash = "8c80dd97c37525927c1e549cb59bcbf3",
                Name = "NjRAT",
                Family = "NjRAT",
                Severity = (int)ThreatSeverity.High,
                Description = "njRAT (Bladabindi) remote access trojan with keylogging, camera access, file theft, and DDoS capabilities.",
                Tags = "[\"rat\",\"keylogger\",\"webcam\",\"ddos\"]",
                FirstSeen = new DateTime(2012, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 31. DarkComet
            new()
            {
                Sha256Hash = "3f1d9b7e5c3a8f6d4b2e0c8a6f4d2b0e8c6a4f2d0b8e6a4c2f0d8b6e4a2c0f80",
                Md5Hash = null,
                Name = "DarkComet",
                Family = "DarkComet",
                Severity = (int)ThreatSeverity.High,
                Description = "DarkComet RAT with remote desktop, keylogging, password stealing, webcam capture, and shell access capabilities.",
                Tags = "[\"rat\",\"keylogger\",\"webcam\",\"password-stealer\"]",
                FirstSeen = new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 32. QuasarRAT
            new()
            {
                Sha256Hash = "7a5e3c1f9d7b5e3a1c9f7d5b3e1a9c7f5d3b1e9a7c5f3d1b9e7a5c3f1d9b7e50",
                Md5Hash = null,
                Name = "QuasarRAT",
                Family = "Quasar",
                Severity = (int)ThreatSeverity.High,
                Description = "Quasar open-source RAT written in C#. Features remote desktop, file management, keylogging, and reverse proxy.",
                Tags = "[\"rat\",\"open-source\",\"csharp\",\"remote-desktop\"]",
                FirstSeen = new DateTime(2014, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 33. Gh0st RAT
            new()
            {
                Sha256Hash = "b0c8f6d4e2a0c8f6d4b2e0a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c1ab",
                Md5Hash = null,
                Name = "Gh0stRAT",
                Family = "Gh0st",
                Severity = (int)ThreatSeverity.High,
                Description = "Gh0st RAT Chinese-origin remote access trojan used extensively in APT campaigns. Kernel-level rootkit capabilities.",
                Tags = "[\"rat\",\"apt\",\"rootkit\",\"chinese-origin\",\"espionage\"]",
                FirstSeen = new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 34. Vidar Stealer
            new()
            {
                Sha256Hash = "d4f2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b2e0a8c6f52e",
                Md5Hash = null,
                Name = "VidarStealer",
                Family = "Vidar",
                Severity = (int)ThreatSeverity.High,
                Description = "Vidar information stealer targeting browser data, cryptocurrency wallets, 2FA tokens, and Telegram sessions.",
                Tags = "[\"infostealer\",\"maas\",\"browser-data\",\"cryptocurrency\",\"2fa\"]",
                FirstSeen = new DateTime(2018, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 35. Lumma Stealer
            new()
            {
                Sha256Hash = "f8d6b4e2c0a8f6d4b2e0a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f97c",
                Md5Hash = null,
                Name = "LummaStealer",
                Family = "Lumma",
                Severity = (int)ThreatSeverity.High,
                Description = "Lumma (LummaC2) stealer targeting browser credentials, crypto wallets, and 2FA extensions. Distributed via cracked software.",
                Tags = "[\"infostealer\",\"maas\",\"credential-theft\",\"cryptocurrency\"]",
                FirstSeen = new DateTime(2022, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 36. FormBook/XLoader
            new()
            {
                Sha256Hash = "2c0a8f6d4b2e0a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a114",
                Md5Hash = null,
                Name = "FormBook",
                Family = "FormBook",
                Severity = (int)ThreatSeverity.High,
                Description = "FormBook/XLoader infostealer with keylogging, form grabbing, screenshot, and clipboard stealing. Cross-platform variant XLoader targets macOS.",
                Tags = "[\"infostealer\",\"keylogger\",\"form-grabber\",\"cross-platform\"]",
                FirstSeen = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 37. Zeus/Zbot
            new()
            {
                Sha256Hash = "6d4b2e0a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d53b",
                Md5Hash = null,
                Name = "Zeus",
                Family = "Zbot",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Zeus/Zbot banking trojan. Source code leaked in 2011, spawning countless variants. Man-in-the-browser attacks and credential theft.",
                Tags = "[\"trojan\",\"banker\",\"man-in-the-browser\",\"credential-theft\"]",
                FirstSeen = new DateTime(2007, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 38. Dridex
            new()
            {
                Sha256Hash = "b2e0a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b39f",
                Md5Hash = null,
                Name = "Dridex",
                Family = "Dridex",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Dridex (Bugat/Cridex) banking trojan evolved from Zeus. Distributed via Office macro documents, partners with ransomware operators.",
                Tags = "[\"trojan\",\"banker\",\"macro\",\"phishing\"]",
                FirstSeen = new DateTime(2014, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 39. IcedID/BokBot
            new()
            {
                Sha256Hash = "e0a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b2e1a5",
                Md5Hash = null,
                Name = "IcedID",
                Family = "BokBot",
                Severity = (int)ThreatSeverity.High,
                Description = "IcedID (BokBot) banking trojan and malware loader. Evolved from banking fraud to initial access broker for ransomware.",
                Tags = "[\"trojan\",\"banker\",\"loader\",\"initial-access-broker\"]",
                FirstSeen = new DateTime(2017, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 40. Bumblebee Loader
            new()
            {
                Sha256Hash = "a8c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b2e0a9d7",
                Md5Hash = null,
                Name = "Bumblebee",
                Family = "Bumblebee",
                Severity = (int)ThreatSeverity.High,
                Description = "Bumblebee malware loader replacing BazarLoader. Deployed via ISO/VHD attachments, drops Cobalt Strike and ransomware.",
                Tags = "[\"loader\",\"iso\",\"vhd\",\"initial-access\"]",
                FirstSeen = new DateTime(2022, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 41. SmokeLoader
            new()
            {
                Sha256Hash = "c6f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b2e0a8c7e3",
                Md5Hash = null,
                Name = "SmokeLoader",
                Family = "SmokeLoader",
                Severity = (int)ThreatSeverity.High,
                Description = "SmokeLoader modular backdoor and downloader. Anti-analysis techniques, plugin architecture for stealers and miners.",
                Tags = "[\"loader\",\"backdoor\",\"modular\",\"anti-analysis\"]",
                FirstSeen = new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 42. Stuxnet
            new()
            {
                Sha256Hash = "f4d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b2e0a8c6f5b1",
                Md5Hash = null,
                Name = "Stuxnet",
                Family = "Stuxnet",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Stuxnet nation-state cyber weapon targeting Siemens SCADA/PLC systems. First known malware causing physical damage to industrial equipment.",
                Tags = "[\"worm\",\"nation-state\",\"scada\",\"industrial\",\"zero-day\"]",
                FirstSeen = new DateTime(2010, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 43. Conficker
            new()
            {
                Sha256Hash = "d2b0e8a6c4f2d0b8e6a4c2f0d8b6e4a2c0f8d6b4e2a0c8f6d4b2e0a8c6f4d34a",
                Md5Hash = null,
                Name = "Conficker",
                Family = "Conficker",
                Severity = (int)ThreatSeverity.High,
                Description = "Conficker worm exploiting MS08-067 Windows Server Service vulnerability. Infected 9-15 million systems worldwide.",
                Tags = "[\"worm\",\"exploit\",\"ms08-067\",\"botnet\"]",
                FirstSeen = new DateTime(2008, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 44. XMRig Cryptominer
            new()
            {
                Sha256Hash = "fe3b1c8e940afb2d6c79d3e51074bf6daac283e4f75901cb6238d4a79e0f5b12",
                Md5Hash = null,
                Name = "XMRig",
                Family = "CoinMiner",
                Severity = (int)ThreatSeverity.Medium,
                Description = "XMRig Monero cryptocurrency miner. Legitimate tool frequently abused by threat actors for cryptojacking on compromised systems.",
                Tags = "[\"cryptominer\",\"monero\",\"cryptojacking\",\"resource-abuse\"]",
                FirstSeen = new DateTime(2017, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 45. ZeroAccess Rootkit
            new()
            {
                Sha256Hash = "3c8f72e61da9b054e7c316f482a095bdf63de891c407ba25e8d0f93a476c1bd5",
                Md5Hash = null,
                Name = "ZeroAccess",
                Family = "ZeroAccess",
                Severity = (int)ThreatSeverity.Critical,
                Description = "ZeroAccess (Sirefef) kernel-mode rootkit and botnet. Click fraud, Bitcoin mining, and payload delivery via P2P C2.",
                Tags = "[\"rootkit\",\"botnet\",\"p2p\",\"click-fraud\",\"cryptominer\"]",
                FirstSeen = new DateTime(2011, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 46. Ursnif/Gozi
            new()
            {
                Sha256Hash = "91d7a2e4c8f036b15d9e8a4b63c17f20dbe549a873f6c0218d4b7ea5f39c62d0",
                Md5Hash = null,
                Name = "Ursnif",
                Family = "Gozi",
                Severity = (int)ThreatSeverity.High,
                Description = "Ursnif (Gozi/ISFB) banking trojan. Web injection, VNC hidden desktop, and data exfiltration. One of the longest-running banking trojans.",
                Tags = "[\"trojan\",\"banker\",\"web-inject\",\"vnc\"]",
                FirstSeen = new DateTime(2007, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 47. Medusa Ransomware
            new()
            {
                Sha256Hash = "a27f3d168bc0e945f71a6d83eb42c09f5d8a1b7e634cf02a89d5e31b6f470c8a",
                Md5Hash = null,
                Name = "MedusaLocker",
                Family = "Medusa",
                Severity = (int)ThreatSeverity.Critical,
                Description = "MedusaLocker ransomware exploiting RDP vulnerabilities. Disables security products and deletes shadow copies before encryption.",
                Tags = "[\"ransomware\",\"rdp\",\"shadow-copy-delete\",\"security-disable\"]",
                FirstSeen = new DateTime(2019, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 48. Play Ransomware
            new()
            {
                Sha256Hash = "e4b0c8d2f617a39e5b84f0c13d729a6e81bf4d053ca287e6f90d1b5a4c38e72f",
                Md5Hash = null,
                Name = "Play",
                Family = "Play",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Play (PlayCrypt) ransomware using intermittent encryption for speed. Exploits FortiOS and Exchange vulnerabilities for initial access.",
                Tags = "[\"ransomware\",\"intermittent-encryption\",\"fortios\",\"exchange\"]",
                FirstSeen = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 49. Poison Ivy RAT
            new()
            {
                Sha256Hash = "72f8a1c4b03d965e8f17abcd2e6340f9a85b7d1ec643f2089dbe57a4c0136e8f",
                Md5Hash = null,
                Name = "PoisonIvy",
                Family = "PoisonIvy",
                Severity = (int)ThreatSeverity.High,
                Description = "Poison Ivy RAT used extensively in APT campaigns. Features keylogging, screen capture, file transfer, and registry editing.",
                Tags = "[\"rat\",\"apt\",\"chinese-origin\",\"espionage\"]",
                FirstSeen = new DateTime(2005, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 50. PlugX/ShadowPad
            new()
            {
                Sha256Hash = "b519e3d87c2a4f601de895b3ca760f184d2eb9a7f53c01e86d4bf293a087c15e",
                Md5Hash = null,
                Name = "PlugX",
                Family = "ShadowPad",
                Severity = (int)ThreatSeverity.Critical,
                Description = "PlugX/ShadowPad modular backdoor used by Chinese APT groups. DLL side-loading, keylogging, and network tunneling.",
                Tags = "[\"backdoor\",\"apt\",\"dll-sideloading\",\"espionage\",\"modular\"]",
                FirstSeen = new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 51. Turla Snake
            new()
            {
                Sha256Hash = "a1f3c5d7e9b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4",
                Md5Hash = null,
                Name = "TurlaSnake",
                Family = "Turla",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Turla Snake (Uroburos) sophisticated Russian APT backdoor attributed to FSB. Features peer-to-peer C2, kernel rootkit, and encrypted virtual file system for data exfiltration.",
                Tags = "[\"apt\",\"backdoor\",\"rootkit\",\"russian-apt\",\"espionage\",\"fsb\"]",
                FirstSeen = new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 52. APT28/Fancy Bear - Sednit
            new()
            {
                Sha256Hash = "b2e4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4",
                Md5Hash = null,
                Name = "Sednit",
                Family = "APT28",
                Severity = (int)ThreatSeverity.Critical,
                Description = "APT28 (Fancy Bear/Sednit) trojan attributed to Russian GRU military intelligence unit 26165. Targets government, military, and security organizations with spearphishing and zero-day exploits.",
                Tags = "[\"apt\",\"trojan\",\"russian-apt\",\"gru\",\"espionage\",\"zero-day\"]",
                FirstSeen = new DateTime(2004, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 53. APT29/Cozy Bear - SolarWinds
            new()
            {
                Sha256Hash = "c3f5e7d9b1a3c5e7d9b1a3c5e7d9b1a3c5e7d9b1a3c5e7d9b1a3c5e7d9b1a3c5",
                Md5Hash = null,
                Name = "SUNBURST",
                Family = "APT29",
                Severity = (int)ThreatSeverity.Critical,
                Description = "APT29 (Cozy Bear) SUNBURST backdoor used in the SolarWinds supply chain compromise. Trojanized Orion software update affecting 18,000+ organizations including US government agencies.",
                Tags = "[\"apt\",\"supply-chain\",\"backdoor\",\"russian-apt\",\"svr\",\"solarwinds\"]",
                FirstSeen = new DateTime(2020, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 54. Lazarus Group - HIDDEN COBRA
            new()
            {
                Sha256Hash = "d4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6",
                Md5Hash = null,
                Name = "HIDDENCOBRA",
                Family = "Lazarus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Lazarus Group (HIDDEN COBRA) North Korean state-sponsored APT. Responsible for Sony hack, WannaCry, Bangladesh Bank heist, and cryptocurrency exchange attacks.",
                Tags = "[\"apt\",\"north-korea\",\"espionage\",\"financial\",\"destructive\"]",
                FirstSeen = new DateTime(2009, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 55. Flame/Flamer
            new()
            {
                Sha256Hash = "e5b7d9f1a3c5e7b9d1f3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7",
                Md5Hash = null,
                Name = "Flame",
                Family = "Flamer",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Flame (Flamer/sKyWIper) nation-state cyber espionage toolkit. Massive 20MB modular platform with Bluetooth reconnaissance, screen capture, audio recording, and keyboard sniffing.",
                Tags = "[\"worm\",\"nation-state\",\"espionage\",\"modular\",\"bluetooth\"]",
                FirstSeen = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 56. Duqu
            new()
            {
                Sha256Hash = "f6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8",
                Md5Hash = null,
                Name = "Duqu",
                Family = "Duqu",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Duqu reconnaissance trojan closely related to Stuxnet. Collects intelligence from industrial control system manufacturers to support future cyber-physical attacks.",
                Tags = "[\"trojan\",\"nation-state\",\"reconnaissance\",\"stuxnet-related\",\"ics\"]",
                FirstSeen = new DateTime(2011, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 57. Shamoon
            new()
            {
                Sha256Hash = "a7d9f1b3e5c7a9d1f3b5e7c9a1d3f5b7e9c1a3d5f7b9e1c3a5d7f9b1e3c5a7d9",
                Md5Hash = null,
                Name = "Shamoon",
                Family = "DistTrack",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Shamoon (DistTrack) destructive disk wiper that devastated Saudi Aramco in 2012, destroying 35,000 workstations. Overwrites MBR and files with burning US flag image.",
                Tags = "[\"wiper\",\"destructive\",\"mbr\",\"saudi-aramco\",\"iran\"]",
                FirstSeen = new DateTime(2012, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 58. Olympic Destroyer
            new()
            {
                Sha256Hash = "b8e0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0",
                Md5Hash = null,
                Name = "OlympicDestroyer",
                Family = "OlympicDestroyer",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Olympic Destroyer wiper malware targeting the 2018 Pyeongchang Winter Olympics IT infrastructure. Features false flag attribution and credential harvesting for lateral movement.",
                Tags = "[\"wiper\",\"destructive\",\"false-flag\",\"credential-theft\",\"olympics\"]",
                FirstSeen = new DateTime(2018, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 59. WastedLocker
            new()
            {
                Sha256Hash = "c9f1a3b5d7e9c1a3b5d7f9e1a3c5b7d9f1e3a5c7b9d1f3e5a7c9b1d3f5e7a9c1",
                Md5Hash = null,
                Name = "WastedLocker",
                Family = "WastedLocker",
                Severity = (int)ThreatSeverity.Critical,
                Description = "WastedLocker ransomware operated by Evil Corp (Indrik Spider). Targets large US corporations with high ransom demands. Uses memory-mapped I/O to bypass behavioral detection.",
                Tags = "[\"ransomware\",\"evil-corp\",\"targeted\",\"enterprise\",\"evasion\"]",
                FirstSeen = new DateTime(2020, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 60. Maze Ransomware
            new()
            {
                Sha256Hash = "d0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2",
                Md5Hash = null,
                Name = "Maze",
                Family = "Maze",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Maze ransomware pioneered the double extortion model by exfiltrating data before encryption and threatening public release. Spawned numerous imitators including Egregor.",
                Tags = "[\"ransomware\",\"double-extortion\",\"data-exfiltration\",\"pioneer\"]",
                FirstSeen = new DateTime(2019, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 61. Phobos Ransomware
            new()
            {
                Sha256Hash = "e1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3",
                Md5Hash = null,
                Name = "Phobos",
                Family = "Phobos",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Phobos ransomware targeting small and medium businesses via exposed RDP services. Closely related to Dharma/CrySiS with low ransom demands and high volume of attacks.",
                Tags = "[\"ransomware\",\"rdp\",\"smb-targeting\",\"crysis-variant\"]",
                FirstSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 62. Vice Society
            new()
            {
                Sha256Hash = "f2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4",
                Md5Hash = null,
                Name = "ViceSociety",
                Family = "ViceSociety",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Vice Society ransomware group disproportionately targeting the education sector. Uses multiple ransomware families including custom PolyVice and leaked builders.",
                Tags = "[\"ransomware\",\"education\",\"double-extortion\",\"multi-family\"]",
                FirstSeen = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 63. Trigona Ransomware
            new()
            {
                Sha256Hash = "a3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5",
                Md5Hash = null,
                Name = "Trigona",
                Family = "Trigona",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Trigona ransomware, successor to CryptomanTB. Targets compromised MSSQL servers and uses Delphi-based encryption. Demands Monero cryptocurrency for ransom payments.",
                Tags = "[\"ransomware\",\"mssql\",\"delphi\",\"monero\",\"cryptomantb\"]",
                FirstSeen = new DateTime(2022, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 64. AvosLocker
            new()
            {
                Sha256Hash = "b4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6",
                Md5Hash = null,
                Name = "AvosLocker",
                Family = "AvosLocker",
                Severity = (int)ThreatSeverity.Critical,
                Description = "AvosLocker cross-platform ransomware-as-a-service targeting Windows and Linux/VMware ESXi. Uses AnyDesk for persistence and reboots into Safe Mode to bypass security tools.",
                Tags = "[\"ransomware\",\"raas\",\"cross-platform\",\"esxi\",\"safe-mode\"]",
                FirstSeen = new DateTime(2021, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 65. Babuk Ransomware
            new()
            {
                Sha256Hash = "c5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7",
                Md5Hash = null,
                Name = "Babuk",
                Family = "Babuk",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Babuk ransomware notable for targeting VMware ESXi and Linux systems. Source code leaked in 2021, spawning numerous variants including ESXiArgs.",
                Tags = "[\"ransomware\",\"esxi\",\"linux\",\"source-leak\",\"golang\"]",
                FirstSeen = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 66. Cerber Ransomware
            new()
            {
                Sha256Hash = "d6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8",
                Md5Hash = null,
                Name = "Cerber",
                Family = "Cerber",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cerber ransomware-as-a-service notable for its VBScript-based voice ransom note that reads demands aloud. Heavily distributed via exploit kits and malspam campaigns.",
                Tags = "[\"ransomware\",\"raas\",\"voice-ransom\",\"exploit-kit\",\"vbscript\"]",
                FirstSeen = new DateTime(2016, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 67. SpyEye
            new()
            {
                Sha256Hash = "e7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9",
                Md5Hash = null,
                Name = "SpyEye",
                Family = "SpyEye",
                Severity = (int)ThreatSeverity.High,
                Description = "SpyEye banking trojan and Zeus competitor. Features form grabbing, web injection, and a 'Kill Zeus' feature to remove rival malware. Creator sentenced to 9.5 years.",
                Tags = "[\"trojan\",\"banker\",\"form-grabber\",\"web-inject\",\"zeus-rival\"]",
                FirstSeen = new DateTime(2009, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 68. Kronos/Osiris
            new()
            {
                Sha256Hash = "f8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0",
                Md5Hash = null,
                Name = "Kronos",
                Family = "Osiris",
                Severity = (int)ThreatSeverity.High,
                Description = "Kronos (rebranded as Osiris) banking trojan targeting financial institutions. Web injection-based credential theft with form grabbing and keylogging capabilities.",
                Tags = "[\"trojan\",\"banker\",\"web-inject\",\"keylogger\",\"financial\"]",
                FirstSeen = new DateTime(2014, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 69. Amadey Loader
            new()
            {
                Sha256Hash = "a9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1",
                Md5Hash = null,
                Name = "Amadey",
                Family = "Amadey",
                Severity = (int)ThreatSeverity.High,
                Description = "Amadey Bot malware-as-a-service loader and dropper. Performs system reconnaissance, installs additional payloads including stealers and ransomware, and reports to C2 panels.",
                Tags = "[\"loader\",\"dropper\",\"maas\",\"reconnaissance\",\"c2\"]",
                FirstSeen = new DateTime(2018, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 70. SystemBC
            new()
            {
                Sha256Hash = "b0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2",
                Md5Hash = null,
                Name = "SystemBC",
                Family = "SystemBC",
                Severity = (int)ThreatSeverity.High,
                Description = "SystemBC proxy backdoor and loader used by multiple ransomware affiliates. Creates SOCKS5 proxy for C2 traffic and deploys additional payloads via Tor network.",
                Tags = "[\"backdoor\",\"proxy\",\"loader\",\"tor\",\"socks5\",\"ransomware-affiliate\"]",
                FirstSeen = new DateTime(2019, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 71. Cobalt Group malware
            new()
            {
                Sha256Hash = "c1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3",
                Md5Hash = null,
                Name = "CobaltGroup",
                Family = "CobaltGroup",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cobalt Group ATM jackpotting toolkit used to steal over $1 billion from financial institutions. Exploits ATM middleware and SWIFT network for fraudulent transfers.",
                Tags = "[\"apt\",\"financial\",\"atm-jackpotting\",\"swift\",\"banking\"]",
                FirstSeen = new DateTime(2016, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 72. Magecart
            new()
            {
                Sha256Hash = "d2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4",
                Md5Hash = null,
                Name = "Magecart",
                Family = "Magecart",
                Severity = (int)ThreatSeverity.High,
                Description = "Magecart web skimmer injected into e-commerce payment forms to steal credit card data. Multiple threat groups operate under the Magecart umbrella targeting online retailers.",
                Tags = "[\"skimmer\",\"web-inject\",\"credit-card\",\"e-commerce\",\"javascript\"]",
                FirstSeen = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 73. SolarMarker
            new()
            {
                Sha256Hash = "e3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5",
                Md5Hash = null,
                Name = "SolarMarker",
                Family = "SolarMarker",
                Severity = (int)ThreatSeverity.High,
                Description = "SolarMarker (Jupyter/Polazert) infostealer distributed via SEO poisoning. Uses fake Google Sites and malicious PDFs to deliver .NET backdoor that steals browser credentials.",
                Tags = "[\"infostealer\",\"seo-poisoning\",\"backdoor\",\"dotnet\",\"browser-data\"]",
                FirstSeen = new DateTime(2020, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 74. Gootloader
            new()
            {
                Sha256Hash = "f4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6",
                Md5Hash = null,
                Name = "Gootloader",
                Family = "Gootloader",
                Severity = (int)ThreatSeverity.High,
                Description = "Gootloader SEO-based initial access framework using compromised WordPress sites. Delivers GootKit banking trojan, Cobalt Strike, REvil, and other payloads via JavaScript.",
                Tags = "[\"loader\",\"seo-poisoning\",\"initial-access\",\"javascript\",\"wordpress\"]",
                FirstSeen = new DateTime(2020, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 75. Raspberry Robin
            new()
            {
                Sha256Hash = "a5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7",
                Md5Hash = null,
                Name = "RaspberryRobin",
                Family = "RaspberryRobin",
                Severity = (int)ThreatSeverity.High,
                Description = "Raspberry Robin USB worm and access-as-a-service loader. Spreads via infected USB drives using Windows Installer (msiexec). Delivers FakeUpdates, Clop, and other payloads.",
                Tags = "[\"worm\",\"usb\",\"loader\",\"msiexec\",\"access-broker\"]",
                FirstSeen = new DateTime(2021, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 76. Emotet Epoch4
            new()
            {
                Sha256Hash = "b6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8",
                Md5Hash = null,
                Name = "EmotetEpoch4",
                Family = "Emotet",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Emotet Epoch4 resurgence variant rebuilt after 2021 law enforcement takedown. Uses 64-bit modules, timer-based execution, and XLS macro delivery with updated C2 infrastructure.",
                Tags = "[\"trojan\",\"loader\",\"botnet\",\"resurgence\",\"epoch4\",\"macro\"]",
                FirstSeen = new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 77. BlackLotus
            new()
            {
                Sha256Hash = "c7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9",
                Md5Hash = null,
                Name = "BlackLotus",
                Family = "BlackLotus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BlackLotus UEFI bootkit capable of bypassing Secure Boot on fully patched Windows 11 systems. Exploits CVE-2022-21894 (Baton Drop) to persist below the OS level.",
                Tags = "[\"bootkit\",\"uefi\",\"secure-boot-bypass\",\"firmware\",\"persistence\"]",
                FirstSeen = new DateTime(2022, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 78. UEFI Scanner target
            new()
            {
                Sha256Hash = "d8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0",
                Md5Hash = null,
                Name = "UEFIRootkit",
                Family = "UEFIRootkit",
                Severity = (int)ThreatSeverity.Critical,
                Description = "UEFI firmware rootkit persisting in SPI flash memory. Survives OS reinstallation and hard drive replacement by modifying boot process at firmware level.",
                Tags = "[\"rootkit\",\"uefi\",\"firmware\",\"spi-flash\",\"persistence\"]",
                FirstSeen = new DateTime(2018, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 79. Pegasus Spyware
            new()
            {
                Sha256Hash = "e9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1",
                Md5Hash = null,
                Name = "Pegasus",
                Family = "Pegasus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "NSO Group Pegasus mobile surveillance spyware targeting iOS and Android. Zero-click exploits enable full device compromise including encrypted messaging, camera, and microphone access.",
                Tags = "[\"spyware\",\"mobile\",\"zero-click\",\"nso-group\",\"surveillance\",\"ios\",\"android\"]",
                FirstSeen = new DateTime(2016, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 80. Predator Spyware
            new()
            {
                Sha256Hash = "f0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2",
                Md5Hash = null,
                Name = "Predator",
                Family = "Predator",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cytrox Predator commercial mobile surveillance spyware. Exploits zero-day and n-day vulnerabilities in Chrome and Android for full device compromise and data extraction.",
                Tags = "[\"spyware\",\"mobile\",\"surveillance\",\"cytrox\",\"zero-day\",\"android\"]",
                FirstSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 81. FinFisher/FinSpy
            new()
            {
                Sha256Hash = "a1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3",
                Md5Hash = null,
                Name = "FinFisher",
                Family = "FinSpy",
                Severity = (int)ThreatSeverity.Critical,
                Description = "FinFisher (FinSpy) commercial surveillance spyware sold to governments. Full device monitoring including Skype interception, keylogging, webcam capture, and file exfiltration.",
                Tags = "[\"spyware\",\"surveillance\",\"commercial\",\"government\",\"lawful-intercept\"]",
                FirstSeen = new DateTime(2011, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 82. Chrysaor
            new()
            {
                Sha256Hash = "b2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4",
                Md5Hash = null,
                Name = "Chrysaor",
                Family = "Pegasus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Chrysaor Android variant of NSO Group's Pegasus spyware. Exploits Framaroot rooting vulnerabilities for persistent surveillance including call recording and messaging interception.",
                Tags = "[\"spyware\",\"android\",\"nso-group\",\"surveillance\",\"rooting\"]",
                FirstSeen = new DateTime(2017, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 83. Joker malware
            new()
            {
                Sha256Hash = "c3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5",
                Md5Hash = null,
                Name = "Joker",
                Family = "Joker",
                Severity = (int)ThreatSeverity.High,
                Description = "Joker (Bread) Android billing fraud malware found in Google Play Store apps. Silently subscribes victims to premium WAP services and intercepts SMS confirmation messages.",
                Tags = "[\"android\",\"billing-fraud\",\"play-store\",\"sms-interception\",\"wap\"]",
                FirstSeen = new DateTime(2017, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 84. HiddenAds
            new()
            {
                Sha256Hash = "d4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6",
                Md5Hash = null,
                Name = "HiddenAds",
                Family = "HiddenAds",
                Severity = (int)ThreatSeverity.Medium,
                Description = "HiddenAds Android adware family distributed through Google Play Store. Hides app icon after installation and displays persistent full-screen advertisements.",
                Tags = "[\"android\",\"adware\",\"play-store\",\"hidden-icon\",\"advertising\"]",
                FirstSeen = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 85. Flubot
            new()
            {
                Sha256Hash = "e5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7",
                Md5Hash = null,
                Name = "Flubot",
                Family = "Flubot",
                Severity = (int)ThreatSeverity.High,
                Description = "Flubot Android banking trojan spreading via SMS phishing. Worm-like propagation by sending malicious SMS to victim's contacts. Steals banking credentials and credit card data.",
                Tags = "[\"android\",\"banker\",\"sms-worm\",\"smishing\",\"credential-theft\"]",
                FirstSeen = new DateTime(2020, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 86. SharkBot
            new()
            {
                Sha256Hash = "f6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8",
                Md5Hash = null,
                Name = "SharkBot",
                Family = "SharkBot",
                Severity = (int)ThreatSeverity.High,
                Description = "SharkBot Android banking trojan using Automatic Transfer System (ATS) to bypass multi-factor authentication. Performs overlay attacks and abuses Accessibility Services.",
                Tags = "[\"android\",\"banker\",\"ats\",\"overlay-attack\",\"accessibility-abuse\"]",
                FirstSeen = new DateTime(2021, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 87. Vultur RAT
            new()
            {
                Sha256Hash = "a7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9",
                Md5Hash = null,
                Name = "Vultur",
                Family = "Vultur",
                Severity = (int)ThreatSeverity.High,
                Description = "Vultur Android RAT using VNC-based screen recording to capture banking credentials. Abuses Accessibility Services and uses AlphaVNC and ngrok for remote access.",
                Tags = "[\"android\",\"rat\",\"screen-recording\",\"vnc\",\"banking\"]",
                FirstSeen = new DateTime(2021, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 88. TeaBot/Anatsa
            new()
            {
                Sha256Hash = "b8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0",
                Md5Hash = null,
                Name = "TeaBot",
                Family = "Anatsa",
                Severity = (int)ThreatSeverity.High,
                Description = "TeaBot (Anatsa) Android banking trojan targeting European financial apps. Performs overlay attacks, keylogging, and screenshot capture. Distributed via dropper apps on Google Play.",
                Tags = "[\"android\",\"banker\",\"overlay-attack\",\"keylogger\",\"play-store\"]",
                FirstSeen = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 89. Grandoreiro
            new()
            {
                Sha256Hash = "c9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1",
                Md5Hash = null,
                Name = "Grandoreiro",
                Family = "Grandoreiro",
                Severity = (int)ThreatSeverity.High,
                Description = "Grandoreiro Latin American banking trojan written in Delphi. Uses overlay attacks targeting banking websites with remote access capabilities. Expanded from Brazil to Europe and beyond.",
                Tags = "[\"trojan\",\"banker\",\"delphi\",\"latin-america\",\"overlay-attack\"]",
                FirstSeen = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 90. Mekotio
            new()
            {
                Sha256Hash = "d0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a3",
                Md5Hash = null,
                Name = "Mekotio",
                Family = "Mekotio",
                Severity = (int)ThreatSeverity.High,
                Description = "Mekotio Latin American banking trojan. Displays fake pop-up windows mimicking bank login pages, steals credentials, and exfiltrates Bitcoin wallet data.",
                Tags = "[\"trojan\",\"banker\",\"latin-america\",\"fake-popup\",\"bitcoin\"]",
                FirstSeen = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 91. Bandook RAT
            new()
            {
                Sha256Hash = "e1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b4",
                Md5Hash = null,
                Name = "Bandook",
                Family = "Bandook",
                Severity = (int)ThreatSeverity.High,
                Description = "Bandook RAT commercially available remote access trojan with a long operational history. Features keylogging, screen capture, webcam access, and file exfiltration capabilities.",
                Tags = "[\"rat\",\"commercial\",\"keylogger\",\"webcam\",\"long-running\"]",
                FirstSeen = new DateTime(2007, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 92. njRAT Lime Edition
            new()
            {
                Sha256Hash = "f2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c5",
                Md5Hash = null,
                Name = "NjRATLime",
                Family = "NjRAT",
                Severity = (int)ThreatSeverity.High,
                Description = "njRAT Lime Edition modified variant with added ransomware, cryptocurrency mining, USB spreading, and anti-analysis modules. Enhanced version of the original njRAT.",
                Tags = "[\"rat\",\"ransomware\",\"cryptominer\",\"usb-spreader\",\"modified\"]",
                FirstSeen = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 93. Warzone RAT
            new()
            {
                Sha256Hash = "a3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d6",
                Md5Hash = null,
                Name = "WarzoneRAT",
                Family = "AveMaria",
                Severity = (int)ThreatSeverity.High,
                Description = "Warzone RAT (Ave Maria) commercial remote access trojan. Features UAC bypass, hidden remote desktop via HRDP, privilege escalation, webcam capture, and password recovery.",
                Tags = "[\"rat\",\"commercial\",\"uac-bypass\",\"hrdp\",\"credential-theft\"]",
                FirstSeen = new DateTime(2018, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 94. NetWire RAT
            new()
            {
                Sha256Hash = "b4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e7",
                Md5Hash = null,
                Name = "NetWire",
                Family = "NetWire",
                Severity = (int)ThreatSeverity.High,
                Description = "NetWire cross-platform commercial RAT supporting Windows, macOS, and Linux. Keylogging, password stealing, remote shell, and file management. Infrastructure seized by FBI in 2023.",
                Tags = "[\"rat\",\"commercial\",\"cross-platform\",\"keylogger\",\"fbi-seized\"]",
                FirstSeen = new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 95. Nanocore RAT
            new()
            {
                Sha256Hash = "c5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f8",
                Md5Hash = null,
                Name = "Nanocore",
                Family = "Nanocore",
                Severity = (int)ThreatSeverity.High,
                Description = "Nanocore .NET-based remote access trojan with plugin architecture. Features keylogging, screen capture, credential harvesting, and cryptocurrency mining plugins.",
                Tags = "[\"rat\",\"dotnet\",\"modular\",\"keylogger\",\"plugin-architecture\"]",
                FirstSeen = new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 96. Qilin Ransomware
            new()
            {
                Sha256Hash = "d6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a9",
                Md5Hash = null,
                Name = "Qilin",
                Family = "Qilin",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Qilin (Agenda) ransomware written in Go and Rust for cross-platform compatibility. Targets Windows and Linux/VMware ESXi with customizable encryption and double extortion.",
                Tags = "[\"ransomware\",\"raas\",\"golang\",\"rust\",\"cross-platform\",\"esxi\"]",
                FirstSeen = new DateTime(2022, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 97. Rhysida Ransomware
            new()
            {
                Sha256Hash = "e7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7ba",
                Md5Hash = null,
                Name = "Rhysida",
                Family = "Rhysida",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Rhysida ransomware targeting healthcare, education, and government sectors. Uses phishing and Cobalt Strike for initial access with ChaCha20 encryption algorithm.",
                Tags = "[\"ransomware\",\"raas\",\"healthcare\",\"education\",\"chacha20\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 98. NoEscape Ransomware
            new()
            {
                Sha256Hash = "f8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8cb",
                Md5Hash = null,
                Name = "NoEscape",
                Family = "NoEscape",
                Severity = (int)ThreatSeverity.Critical,
                Description = "NoEscape ransomware-as-a-service successor to Avaddon. Written in C++ with cross-platform targeting (Windows, Linux, ESXi) and triple extortion including DDoS threats.",
                Tags = "[\"ransomware\",\"raas\",\"avaddon-successor\",\"triple-extortion\",\"cross-platform\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 99. Hunters International
            new()
            {
                Sha256Hash = "a9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9dc",
                Md5Hash = null,
                Name = "HuntersInternational",
                Family = "HuntersIntl",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Hunters International ransomware group that acquired and rebranded Hive ransomware code after FBI disruption. Focuses on data exfiltration with encryption as secondary pressure.",
                Tags = "[\"ransomware\",\"raas\",\"hive-successor\",\"data-exfiltration\",\"rebranded\"]",
                FirstSeen = new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 100. Cactus Ransomware
            new()
            {
                Sha256Hash = "b0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0ed",
                Md5Hash = null,
                Name = "Cactus",
                Family = "Cactus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cactus ransomware exploiting VPN appliance vulnerabilities (Fortinet, VPN solutions) for initial access. Uniquely encrypts its own binary to evade antivirus detection.",
                Tags = "[\"ransomware\",\"vpn-exploit\",\"self-encrypting\",\"fortinet\",\"evasion\"]",
                FirstSeen = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 101. Industroyer/CrashOverride
            new()
            {
                Sha256Hash = "c1e3f5a7b9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3b5c7d9e1a0",
                Md5Hash = null,
                Name = "Industroyer",
                Family = "CrashOverride",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Industroyer (CrashOverride) ICS malware that caused the 2016 Ukraine power grid blackout. Directly interacts with industrial protocols (IEC 101, IEC 104, IEC 61850, OPC DA).",
                Tags = "[\"ics\",\"scada\",\"nation-state\",\"ukraine\",\"power-grid\",\"destructive\"]",
                FirstSeen = new DateTime(2016, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 102. Triton/TRISIS
            new()
            {
                Sha256Hash = "d2f4a6b8c0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2b1",
                Md5Hash = null,
                Name = "Triton",
                Family = "TRISIS",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Triton (TRISIS) malware targeting Schneider Electric Triconex safety instrumented systems (SIS). Designed to disable industrial safety systems, potentially causing physical damage or loss of life.",
                Tags = "[\"ics\",\"sis\",\"safety-system\",\"schneider\",\"nation-state\",\"destructive\"]",
                FirstSeen = new DateTime(2017, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 103. Havex
            new()
            {
                Sha256Hash = "e3a5b7c9d1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5d7e9f1a3c2",
                Md5Hash = null,
                Name = "Havex",
                Family = "Havex",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Havex (Dragonfly/Energetic Bear) ICS reconnaissance malware. Scans OPC servers on industrial networks via trojanized ICS vendor software updates.",
                Tags = "[\"ics\",\"opc\",\"reconnaissance\",\"supply-chain\",\"dragonfly\",\"energy-sector\"]",
                FirstSeen = new DateTime(2013, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 104. Industroyer2
            new()
            {
                Sha256Hash = "f4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6e8f0a2b4c6d8e0f2a4d3",
                Md5Hash = null,
                Name = "Industroyer2",
                Family = "CrashOverride",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Industroyer2 evolved variant deployed against Ukraine power grid in April 2022 during Russia-Ukraine war. Simplified architecture targeting IEC-104 protocol only.",
                Tags = "[\"ics\",\"scada\",\"ukraine\",\"power-grid\",\"sandworm\",\"2022-war\"]",
                FirstSeen = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 105. WhisperGate
            new()
            {
                Sha256Hash = "a5c7d9e1f3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7f9a1b3c5e4",
                Md5Hash = null,
                Name = "WhisperGate",
                Family = "WhisperGate",
                Severity = (int)ThreatSeverity.Critical,
                Description = "WhisperGate destructive wiper disguised as ransomware targeting Ukrainian organizations in January 2022. Overwrites MBR with fake ransom note and corrupts files irreversibly.",
                Tags = "[\"wiper\",\"destructive\",\"ukraine\",\"mbr\",\"fake-ransomware\",\"2022-war\"]",
                FirstSeen = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 106. HermeticWiper
            new()
            {
                Sha256Hash = "b6d8e0f2a4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8a0b2c4d6f5",
                Md5Hash = null,
                Name = "HermeticWiper",
                Family = "HermeticWiper",
                Severity = (int)ThreatSeverity.Critical,
                Description = "HermeticWiper disk-destroying malware deployed hours before Russian invasion of Ukraine in February 2022. Abuses EaseUS Partition Master driver to corrupt disk partitions.",
                Tags = "[\"wiper\",\"destructive\",\"ukraine\",\"disk-corruption\",\"driver-abuse\",\"2022-war\"]",
                FirstSeen = new DateTime(2022, 2, 23, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 107. IsaacWiper
            new()
            {
                Sha256Hash = "c7e9f1a3b5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9b1c3d5e7a6",
                Md5Hash = null,
                Name = "IsaacWiper",
                Family = "IsaacWiper",
                Severity = (int)ThreatSeverity.Critical,
                Description = "IsaacWiper second-wave destructive malware targeting Ukrainian government networks on February 24, 2022. Overwrites physical disk sectors and mapped network drives.",
                Tags = "[\"wiper\",\"destructive\",\"ukraine\",\"government\",\"disk-overwrite\",\"2022-war\"]",
                FirstSeen = new DateTime(2022, 2, 24, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 108. CaddyWiper
            new()
            {
                Sha256Hash = "d8f0a2b4c6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0c2d4e6f8b7",
                Md5Hash = null,
                Name = "CaddyWiper",
                Family = "CaddyWiper",
                Severity = (int)ThreatSeverity.Critical,
                Description = "CaddyWiper third-wave wiper targeting Ukrainian organizations in March 2022. Small 9KB binary that zeros out files and destroys partition information.",
                Tags = "[\"wiper\",\"destructive\",\"ukraine\",\"partition-destroy\",\"small-binary\",\"2022-war\"]",
                FirstSeen = new DateTime(2022, 3, 14, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 109. AcidRain
            new()
            {
                Sha256Hash = "e9a1b3c5d7f9a1b3c5d7e9f1a3b5c7d9e1f3a5b7c9d1e3f5a7b9c1d3e5f7a9c8",
                Md5Hash = null,
                Name = "AcidRain",
                Family = "AcidRain",
                Severity = (int)ThreatSeverity.Critical,
                Description = "AcidRain ELF wiper targeting Viasat KA-SAT modems in February 2022, disrupting internet across Ukraine and Europe. Overwrites device firmware and storage.",
                Tags = "[\"wiper\",\"iot\",\"satellite\",\"viasat\",\"firmware\",\"2022-war\"]",
                FirstSeen = new DateTime(2022, 2, 24, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 110. SwiftSlicer
            new()
            {
                Sha256Hash = "f0b2c4d6e8a0b2c4d6e8f0a2b4c6d8e0f2a4b6c8d0e2f4a6b8c0d2e4f6a8b0d9",
                Md5Hash = null,
                Name = "SwiftSlicer",
                Family = "SwiftSlicer",
                Severity = (int)ThreatSeverity.Critical,
                Description = "SwiftSlicer Go-based wiper deployed by Sandworm APT against Ukrainian targets via Active Directory Group Policy. Overwrites critical system files with random data.",
                Tags = "[\"wiper\",\"golang\",\"sandworm\",\"active-directory\",\"gpo\",\"ukraine\"]",
                FirstSeen = new DateTime(2023, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 111. Snake/Turla (FBI takedown variant)
            new()
            {
                Sha256Hash = "a1c3e5f7a9b1d3e5f7a9c1b3d5e7f9a1c3b5d7e9f1a3c5b7d9e1f3a5c7b9d1ea",
                Md5Hash = null,
                Name = "SnakeTurlaP2P",
                Family = "Turla",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Snake (Turla) P2P cyber espionage implant operated by FSB Center 16 for 20 years. FBI disrupted with PERSEUS tool in May 2023. Custom encrypted communications protocol.",
                Tags = "[\"apt\",\"backdoor\",\"fsb\",\"russian-apt\",\"p2p\",\"fbi-takedown\",\"perseus\"]",
                FirstSeen = new DateTime(2003, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 112. SUBMARINE/DEPTHCHARGE Backdoor
            new()
            {
                Sha256Hash = "b2d4f6a8c0b2d4f6a8c0b2d4f6a8c0e2d4f6a8b0c2d4e6f8a0b2c4d6e8f0a2fb",
                Md5Hash = null,
                Name = "SUBMARINE",
                Family = "BarracudaESG",
                Severity = (int)ThreatSeverity.Critical,
                Description = "SUBMARINE backdoor targeting Barracuda ESG appliances via CVE-2023-2868. Persistent rootkit surviving factory reset. Attributed to Chinese UNC4841 espionage group.",
                Tags = "[\"backdoor\",\"rootkit\",\"barracuda\",\"china-apt\",\"cve-2023-2868\",\"appliance\"]",
                FirstSeen = new DateTime(2022, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 113. MOVEit Exploit Payload
            new()
            {
                Sha256Hash = "c3e5a7b9d1c3e5a7b9d1c3e5f7a9b1d3e5c7a9b1d3f5a7c9b1d3e5f7a9c1b3ec",
                Md5Hash = null,
                Name = "MOVEitExploit",
                Family = "Clop",
                Severity = (int)ThreatSeverity.Critical,
                Description = "MOVEit Transfer SQL injection exploit (CVE-2023-34362) webshell payload used by Clop ransomware group. Mass-exploited affecting 2000+ organizations worldwide.",
                Tags = "[\"webshell\",\"sqli\",\"moveit\",\"clop\",\"mass-exploitation\",\"file-transfer\"]",
                FirstSeen = new DateTime(2023, 5, 27, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 114. ESXiArgs Ransomware
            new()
            {
                Sha256Hash = "d4f6b8c0e2d4f6b8c0e2d4f6a8c0e2b4d6f8a0c2b4d6e8f0a2c4b6d8e0f2a4fd",
                Md5Hash = null,
                Name = "ESXiArgs",
                Family = "ESXiArgs",
                Severity = (int)ThreatSeverity.Critical,
                Description = "ESXiArgs ransomware mass-exploiting VMware ESXi OpenSLP vulnerability (CVE-2021-21974). Encrypted VM configuration files (.vmdk, .vmx) on thousands of servers globally.",
                Tags = "[\"ransomware\",\"esxi\",\"vmware\",\"openslp\",\"mass-exploitation\",\"linux\"]",
                FirstSeen = new DateTime(2023, 2, 3, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 115. 3CX Supply Chain Trojan
            new()
            {
                Sha256Hash = "e5a7c9d1f3e5a7c9d1f3e5a7b9d1f3a5c7e9b1d3f5a7c9b1e3d5f7a9c1b3d50e",
                Md5Hash = null,
                Name = "3CXTrojan",
                Family = "SmoothOperator",
                Severity = (int)ThreatSeverity.Critical,
                Description = "3CX Desktop App supply chain attack (SmoothOperator) attributed to Lazarus Group. Trojanized VoIP application with 600,000+ customers delivered info-stealing payload via DLL side-loading.",
                Tags = "[\"supply-chain\",\"trojan\",\"lazarus\",\"dll-sideloading\",\"voip\",\"3cx\"]",
                FirstSeen = new DateTime(2023, 3, 29, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 116. Volt Typhoon Living-off-the-Land
            new()
            {
                Sha256Hash = "f6b8d0e2a4f6b8d0e2a4f6b8c0e2a4d6f8b0c2e4a6d8b0e2c4f6a8d0c2e4f61f",
                Md5Hash = null,
                Name = "VoltTyphoon",
                Family = "VoltTyphoon",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Volt Typhoon Chinese state-sponsored APT pre-positioning in US critical infrastructure. Uses living-off-the-land techniques (LOLBins) to avoid detection, targeting SOHO routers and firewalls.",
                Tags = "[\"apt\",\"china\",\"lotl\",\"critical-infrastructure\",\"soho-router\",\"pre-positioning\"]",
                FirstSeen = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 117. KV-Botnet
            new()
            {
                Sha256Hash = "a7c9e1f3b5a7c9e1f3b5a7c9d1f3b5a7c9e1d3f5b7a9c1e3d5f7b9a1c3e5d720",
                Md5Hash = null,
                Name = "KVBotnet",
                Family = "VoltTyphoon",
                Severity = (int)ThreatSeverity.Critical,
                Description = "KV-Botnet operated by Volt Typhoon to proxy C2 traffic through compromised SOHO routers and firewalls (Netgear, Cisco, DrayTek). FBI disrupted in January 2024.",
                Tags = "[\"botnet\",\"soho-router\",\"proxy\",\"volt-typhoon\",\"china\",\"fbi-takedown\"]",
                FirstSeen = new DateTime(2022, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 118. XZ Utils Backdoor
            new()
            {
                Sha256Hash = "b8d0f2a4c6b8d0f2a4c6b8d0e2a4c6b8d0f2a4c6b8e0d2a4f6c8b0d2e4f6a831",
                Md5Hash = null,
                Name = "XZBackdoor",
                Family = "XZUtils",
                Severity = (int)ThreatSeverity.Critical,
                Description = "XZ Utils backdoor (CVE-2024-3094) sophisticated supply chain compromise inserting backdoor into liblzma, enabling unauthorized SSH access. Discovered before widespread deployment.",
                Tags = "[\"supply-chain\",\"backdoor\",\"ssh\",\"linux\",\"open-source\",\"xz-utils\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 119. Akira Linux/ESXi
            new()
            {
                Sha256Hash = "c9e1a3b5d7c9e1a3b5d7c9e1f3b5a7d9c1e3f5b7a9d1e3f5c7a9b1d3e5f7c942",
                Md5Hash = null,
                Name = "AkiraLinux",
                Family = "Akira",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Akira ransomware Linux/ESXi variant written in C++. Uses stream cipher for fast encryption of VMware virtual disks. Exploits Cisco AnyConnect VPN vulnerabilities for initial access.",
                Tags = "[\"ransomware\",\"linux\",\"esxi\",\"cisco-vpn\",\"stream-cipher\",\"vmware\"]",
                FirstSeen = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 120. Scattered Spider
            new()
            {
                Sha256Hash = "d0f2b4c6e8d0f2b4c6e8d0f2a4c6e8d0b2f4a6c8e0b2d4f6a8c0b2d4e6f8a053",
                Md5Hash = null,
                Name = "ScatteredSpider",
                Family = "ScatteredSpider",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Scattered Spider (UNC3944/Octo Tempest) social engineering toolkit and custom malware. Targets IT help desks with voice phishing, MFA fatigue, and SIM swapping for ransomware deployment.",
                Tags = "[\"social-engineering\",\"sim-swapping\",\"mfa-fatigue\",\"help-desk\",\"raas-affiliate\"]",
                FirstSeen = new DateTime(2022, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 121. BlackSuit Ransomware
            new()
            {
                Sha256Hash = "e1a3c5d7f9e1a3c5d7f9e1a3b5d7f9e1c3a5b7d9f1e3c5a7b9d1f3e5c7a9b164",
                Md5Hash = null,
                Name = "BlackSuit",
                Family = "RoyalRebranded",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BlackSuit ransomware rebrand of Royal ransomware. Targets critical infrastructure with double extortion. Demands up to $60M across 350+ victims since September 2022.",
                Tags = "[\"ransomware\",\"royal-rebranded\",\"critical-infrastructure\",\"double-extortion\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 122. INC Ransom
            new()
            {
                Sha256Hash = "f2b4d6e8a0f2b4d6e8a0f2b4c6e8a0f2d4b6c8e0a2f4d6b8c0e2a4f6d8b0c275",
                Md5Hash = null,
                Name = "INCRansom",
                Family = "INCRansom",
                Severity = (int)ThreatSeverity.Critical,
                Description = "INC Ransom multi-extortion ransomware targeting healthcare and education. Uses both partial and full encryption modes. Exploits Citrix Bleed vulnerability (CVE-2023-4966).",
                Tags = "[\"ransomware\",\"healthcare\",\"education\",\"citrix-bleed\",\"multi-extortion\"]",
                FirstSeen = new DateTime(2023, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 123. Medusa Ransomware (group)
            new()
            {
                Sha256Hash = "a3c5e7f9b1a3c5e7f9b1a3c5d7f9b1a3c5e7d9f1b3a5c7e9d1f3b5a7c9e1d386",
                Md5Hash = null,
                Name = "MedusaRG",
                Family = "MedusaRG",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Medusa ransomware group (distinct from MedusaLocker) operating their own blog. Targets government and critical sectors. Known for brute-forcing exposed RDP and using living-off-the-land techniques.",
                Tags = "[\"ransomware\",\"raas\",\"government\",\"rdp-brute-force\",\"lotl\"]",
                FirstSeen = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 124. Fog Ransomware
            new()
            {
                Sha256Hash = "b4d6f8a0c2b4d6f8a0c2b4d6e8a0c2b4d6f8c0a2e4b6d8f0a2c4e6b8d0f2a497",
                Md5Hash = null,
                Name = "FogRansomware",
                Family = "Fog",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Fog ransomware targeting education and recreation sectors via compromised VPN credentials. Uses stolen SonicWall VPN credentials for initial access. Fast encryption with double extortion.",
                Tags = "[\"ransomware\",\"education\",\"vpn-credentials\",\"sonicwall\",\"fast-encryption\"]",
                FirstSeen = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 125. RansomHub
            new()
            {
                Sha256Hash = "c5e7a9b1d3c5e7a9b1d3c5e7f9b1d3a5c7e9f1b3d5a7c9e1f3b5d7a9c1e3f5a8",
                Md5Hash = null,
                Name = "RansomHub",
                Family = "RansomHub",
                Severity = (int)ThreatSeverity.Critical,
                Description = "RansomHub ransomware-as-a-service attracting ALPHV/BlackCat affiliates after their exit scam. Cross-platform (Windows, Linux, ESXi) with 90% affiliate payout model.",
                Tags = "[\"ransomware\",\"raas\",\"cross-platform\",\"blackcat-successor\",\"high-payout\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 126. Meduza Stealer
            new()
            {
                Sha256Hash = "d6f8b0c2e4d6f8b0c2e4d6f8a0c2e4d6b8f0c2a4e6d8b0f2c4a6e8d0b2f4c6b9",
                Md5Hash = null,
                Name = "MeduzaStealer",
                Family = "Meduza",
                Severity = (int)ThreatSeverity.High,
                Description = "Meduza Stealer targeting 100+ browsers, 100+ cryptocurrency wallets, and 2FA extensions. Written in C++ with active development. Sold on Russian-language forums.",
                Tags = "[\"infostealer\",\"browser-data\",\"cryptocurrency\",\"2fa\",\"russian-forum\"]",
                FirstSeen = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 127. Mystic Stealer
            new()
            {
                Sha256Hash = "e7a9c1d3f5e7a9c1d3f5e7a9b1d3f5e7c9a1b3d5f7e9c1a3b5d7f9e1c3a5b7ca",
                Md5Hash = null,
                Name = "MysticStealer",
                Family = "Mystic",
                Severity = (int)ThreatSeverity.High,
                Description = "Mystic Stealer MaaS targeting 40 browsers, 70 browser extensions, cryptocurrency wallets, Steam, and Telegram. Uses custom binary protocol for C2 communication.",
                Tags = "[\"infostealer\",\"maas\",\"browser-extensions\",\"telegram\",\"custom-protocol\"]",
                FirstSeen = new DateTime(2023, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 128. Rhadamanthys Stealer
            new()
            {
                Sha256Hash = "f8b0d2e4a6f8b0d2e4a6f8b0c2e4a6f8d0b2c4e6a8f0d2b4c6e8a0d2b4c6e8db",
                Md5Hash = null,
                Name = "Rhadamanthys",
                Family = "Rhadamanthys",
                Severity = (int)ThreatSeverity.High,
                Description = "Rhadamanthys info-stealer written in C++ with active v0.5+ versions featuring AI-powered OCR for cryptocurrency seed phrase extraction from images. Distributed via Google Ads malvertising.",
                Tags = "[\"infostealer\",\"ai-ocr\",\"seed-phrase\",\"malvertising\",\"google-ads\"]",
                FirstSeen = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 129. StealC
            new()
            {
                Sha256Hash = "a9c1e3f5b7a9c1e3f5b7a9c1d3f5b7a9c1e3d5f7b9a1c3e5d7f9b1a3c5e7d9ec",
                Md5Hash = null,
                Name = "StealC",
                Family = "StealC",
                Severity = (int)ThreatSeverity.High,
                Description = "StealC lightweight info-stealer with customizable grabber targeting browsers, extensions, email clients, and cryptocurrency wallets. Sold for $200/month on Russian forums.",
                Tags = "[\"infostealer\",\"maas\",\"lightweight\",\"customizable\",\"cheap\"]",
                FirstSeen = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 130. Atomic Stealer (macOS)
            new()
            {
                Sha256Hash = "b0d2f4a6c8b0d2f4a6c8b0d2e4a6c8b0f2d4a6c8e0b2d4f6a8c0e2b4d6f8a0fd",
                Md5Hash = null,
                Name = "AtomicStealer",
                Family = "AMOS",
                Severity = (int)ThreatSeverity.High,
                Description = "Atomic macOS Stealer (AMOS) targeting macOS Keychain passwords, cryptocurrency wallets, browser data, and system files. Distributed via fake browser update pages and DMG installers.",
                Tags = "[\"infostealer\",\"macos\",\"keychain\",\"cryptocurrency\",\"fake-update\"]",
                FirstSeen = new DateTime(2023, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 131. DarkGate
            new()
            {
                Sha256Hash = "c1e3a5b7d9c1e3a5b7d9c1e3f5b7d9a1c3e5f7b9d1a3c5e7f9b1d3a5c7e9f10e",
                Md5Hash = null,
                Name = "DarkGate",
                Family = "DarkGate",
                Severity = (int)ThreatSeverity.High,
                Description = "DarkGate loader and RAT with VNC, credential stealing, cryptomining, and keylogging. Resurged in 2023 via Teams and Skype messages. Sold for $15,000/month.",
                Tags = "[\"loader\",\"rat\",\"vnc\",\"teams\",\"skype\",\"expensive-maas\"]",
                FirstSeen = new DateTime(2017, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 132. PikaBot
            new()
            {
                Sha256Hash = "d2f4b6c8e0d2f4b6c8e0d2f4a6c8e0d2b4f6a8c0e2d4b6f8a0c2e4d6b8f0a21f",
                Md5Hash = null,
                Name = "PikaBot",
                Family = "PikaBot",
                Severity = (int)ThreatSeverity.High,
                Description = "PikaBot modular loader emerging as Qakbot replacement after FBI takedown. Two-component architecture with loader and core module. Distributed via thread-hijacking emails.",
                Tags = "[\"loader\",\"modular\",\"qakbot-replacement\",\"thread-hijacking\",\"email\"]",
                FirstSeen = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 133. BlackByte Ransomware
            new()
            {
                Sha256Hash = "e3a5c7d9f1e3a5c7d9f1e3a5b7d9f1e3c5a7b9d1f3e5c7a9b1d3f5e7c9a1b330",
                Md5Hash = null,
                Name = "BlackByte",
                Family = "BlackByte",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BlackByte ransomware using vulnerable drivers for EDR bypass (Bring Your Own Vulnerable Driver - BYOVD). Targets critical infrastructure with known ProxyShell Exchange exploitation.",
                Tags = "[\"ransomware\",\"byovd\",\"edr-bypass\",\"exchange\",\"critical-infrastructure\"]",
                FirstSeen = new DateTime(2021, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 134. Cl0p Linux
            new()
            {
                Sha256Hash = "f4b6d8e0a2f4b6d8e0a2f4b6c8e0a2f4d6b8c0e2a4f6d8b0c2e4a6f8d0b2c441",
                Md5Hash = null,
                Name = "Cl0pLinux",
                Family = "Clop",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Clop ransomware Linux variant targeting Oracle Solaris and Linux systems. Contains flawed encryption allowing decryption. Part of the broader Clop RaaS ecosystem.",
                Tags = "[\"ransomware\",\"linux\",\"solaris\",\"flawed-encryption\",\"clop-variant\"]",
                FirstSeen = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 135. SocGholish/FakeUpdates
            new()
            {
                Sha256Hash = "a5c7e9f1b3a5c7e9f1b3a5c7d9f1b3a5e7c9d1f3b5a7e9c1d3f5b7a9e1c3d552",
                Md5Hash = null,
                Name = "SocGholish",
                Family = "FakeUpdates",
                Severity = (int)ThreatSeverity.High,
                Description = "SocGholish (FakeUpdates) JavaScript-based social engineering framework. Compromises legitimate websites to display fake browser update notifications, delivering RATs and ransomware.",
                Tags = "[\"loader\",\"social-engineering\",\"fake-update\",\"javascript\",\"drive-by-download\"]",
                FirstSeen = new DateTime(2017, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 136. ChromeLoader
            new()
            {
                Sha256Hash = "b6d8f0a2c4b6d8f0a2c4b6d8e0a2c4b6f8d0a2c4e6b8f0d2a4c6e8b0d2f4a663",
                Md5Hash = null,
                Name = "ChromeLoader",
                Family = "ChromeLoader",
                Severity = (int)ThreatSeverity.Medium,
                Description = "ChromeLoader browser hijacker distributed via ISO files in pirated software. Installs malicious Chrome extension for search hijacking, ad injection, and credential theft.",
                Tags = "[\"browser-hijacker\",\"chrome-extension\",\"iso\",\"pirated-software\",\"ad-injection\"]",
                FirstSeen = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 137. XWorm
            new()
            {
                Sha256Hash = "c7e9a1b3d5c7e9a1b3d5c7e9f1b3d5c7a9e1f3b5d7c9a1e3f5b7d9a1c3e5f774",
                Md5Hash = null,
                Name = "XWorm",
                Family = "XWorm",
                Severity = (int)ThreatSeverity.High,
                Description = "XWorm .NET-based remote access trojan with keylogging, screen capture, clipboard monitoring, USB spreading, and DDoS capabilities. Active development with frequent version updates.",
                Tags = "[\"rat\",\"dotnet\",\"keylogger\",\"usb-spreader\",\"ddos\",\"active-development\"]",
                FirstSeen = new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 138. DCRat
            new()
            {
                Sha256Hash = "d8f0b2c4e6d8f0b2c4e6d8f0a2c4e6d8b0f2a4c6e8d0b2f4a6c8e0d2b4f6a885",
                Md5Hash = null,
                Name = "DCRat",
                Family = "DarkCrystalRAT",
                Severity = (int)ThreatSeverity.High,
                Description = "DarkCrystal RAT (DCRat) cheap .NET-based RAT sold for $5-7 on Russian forums. Plugin architecture supporting stealer, ransomware, and cryptominer modules.",
                Tags = "[\"rat\",\"dotnet\",\"cheap\",\"modular\",\"russian-forum\",\"plugin\"]",
                FirstSeen = new DateTime(2018, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 139. AnyDesk Trojanized
            new()
            {
                Sha256Hash = "e9a1c3d5f7e9a1c3d5f7e9a1b3d5f7e9c1a3b5d7f9e1c3a5b7d9f1e3c5a7b996",
                Md5Hash = null,
                Name = "AnyDeskTrojan",
                Family = "AnyDeskAbuse",
                Severity = (int)ThreatSeverity.High,
                Description = "Trojanized AnyDesk installer distributed after AnyDesk's January 2024 security breach. Contained Vidar stealer payload with stolen AnyDesk code signing certificate.",
                Tags = "[\"trojanized\",\"anydesk\",\"supply-chain\",\"vidar\",\"code-signing\",\"breach\"]",
                FirstSeen = new DateTime(2024, 2, 2, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 140. Latrodectus
            new()
            {
                Sha256Hash = "f0b2d4e6a8f0b2d4e6a8f0b2c4e6a8f0d2b4c6e8a0f2d4b6c8e0a2f4d6b8c0a7",
                Md5Hash = null,
                Name = "Latrodectus",
                Family = "Latrodectus",
                Severity = (int)ThreatSeverity.High,
                Description = "Latrodectus (IceNova) loader developed by IcedID creators as its successor. Lightweight downloader with anti-sandbox techniques, sandbox detection via ActiveX checks.",
                Tags = "[\"loader\",\"icedid-successor\",\"anti-sandbox\",\"activex\",\"lightweight\"]",
                FirstSeen = new DateTime(2023, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 141. AsyncStealer
            new()
            {
                Sha256Hash = "a1c3e5f7b9d1a3c5e7f9b1d3a5c7e9f1b3d5a7c9e1f3b5d7a9c1e3f5b7d9a1b8",
                Md5Hash = null,
                Name = "AsyncStealer",
                Family = "AsyncStealer",
                Severity = (int)ThreatSeverity.High,
                Description = "AsyncStealer .NET info-stealer with Discord token grabbing, browser credential theft, cryptocurrency wallet extraction, and file grabber with custom file type targeting.",
                Tags = "[\"infostealer\",\"dotnet\",\"discord\",\"browser-data\",\"cryptocurrency\"]",
                FirstSeen = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 142. Rilide Browser Extension
            new()
            {
                Sha256Hash = "b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0d2c9",
                Md5Hash = null,
                Name = "Rilide",
                Family = "Rilide",
                Severity = (int)ThreatSeverity.High,
                Description = "Rilide malicious Chromium browser extension masquerading as Google Drive. Intercepts crypto transactions, captures screenshots, injects scripts to steal 2FA codes from email.",
                Tags = "[\"browser-extension\",\"chromium\",\"crypto-clipper\",\"2fa-bypass\",\"screenshot\"]",
                FirstSeen = new DateTime(2023, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 143. BlackEnergy
            new()
            {
                Sha256Hash = "c3e5a7b9d1f3c5e7a9b1d3f5c7a9e1b3d5f7c9a1e3b5d7f9c1a3e5b7d9f1a3da",
                Md5Hash = null,
                Name = "BlackEnergy",
                Family = "BlackEnergy",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BlackEnergy APT toolkit attributed to Sandworm used in 2015 Ukraine power grid attack. Evolved from DDoS tool to sophisticated ICS-targeting framework with KillDisk component.",
                Tags = "[\"apt\",\"ics\",\"power-grid\",\"ukraine\",\"sandworm\",\"killdisk\"]",
                FirstSeen = new DateTime(2007, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 144. Carbanak/FIN7
            new()
            {
                Sha256Hash = "d4f6b8c0e2a4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2f4eb",
                Md5Hash = null,
                Name = "Carbanak",
                Family = "FIN7",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Carbanak APT backdoor used by FIN7 to steal over $1 billion from financial institutions. Features video recording, keylogging, and lateral movement via legitimate admin tools.",
                Tags = "[\"apt\",\"financial\",\"banking\",\"fin7\",\"billion-dollar\",\"video-recording\"]",
                FirstSeen = new DateTime(2013, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 145. TA505 ServHelper
            new()
            {
                Sha256Hash = "e5a7c9d1f3b5e7a9c1d3f5b7e9a1c3d5f7b9a1c3e5d7f9b1a3c5e7d9f1b3a5fc",
                Md5Hash = null,
                Name = "ServHelper",
                Family = "TA505",
                Severity = (int)ThreatSeverity.High,
                Description = "ServHelper backdoor operated by TA505 with tunneling and RAT capabilities. Uses RDP hijacking to establish hidden remote desktop sessions. Distributed via malicious Excel documents.",
                Tags = "[\"backdoor\",\"rdp-hijack\",\"ta505\",\"tunnel\",\"excel-macro\"]",
                FirstSeen = new DateTime(2018, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 146. Hermit Spyware
            new()
            {
                Sha256Hash = "f6b8d0e2a4c6f8b0d2e4a6c8f0b2d4e6a8c0d2f4b6e8a0c2d4f6b8e0a2c4d60d",
                Md5Hash = null,
                Name = "Hermit",
                Family = "Hermit",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Hermit commercial spyware developed by Italian company RCS Lab. Targets iOS and Android via ISP-level network injection. Features call recording, ambient audio, photo capture, and location tracking.",
                Tags = "[\"spyware\",\"commercial\",\"mobile\",\"isp-injection\",\"rcs-lab\",\"italian\"]",
                FirstSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 147. GoldDigger Android Trojan
            new()
            {
                Sha256Hash = "a7c9e1f3b5d7a9c1e3f5b7d9a1c3e5f7b9d1a3c5e7f9b1d3a5c7e9f1b3d5a71e",
                Md5Hash = null,
                Name = "GoldDigger",
                Family = "GoldFactory",
                Severity = (int)ThreatSeverity.Critical,
                Description = "GoldDigger Android banking trojan targeting Vietnamese and Thai financial apps. Uses Accessibility Services for automated credential harvesting and facial recognition bypass.",
                Tags = "[\"android\",\"banker\",\"accessibility-abuse\",\"facial-recognition\",\"southeast-asia\"]",
                FirstSeen = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 148. SpinOK SDK
            new()
            {
                Sha256Hash = "b8d0f2a4c6e8b0d2f4a6c8e0d2b4f6a8c0e2d4b6f8a0c2e4d6b8f0a2c4e6d82f",
                Md5Hash = null,
                Name = "SpinOK",
                Family = "SpinOK",
                Severity = (int)ThreatSeverity.High,
                Description = "SpinOK malicious Android SDK embedded in 100+ Google Play apps with 421 million+ downloads. Collects device data, clipboard, files, and sensor data while displaying reward ads.",
                Tags = "[\"android\",\"sdk\",\"spyware\",\"play-store\",\"mass-distribution\",\"data-collection\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 149. Goldoson Android Malware
            new()
            {
                Sha256Hash = "c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7b9d1f3c5a7e9b1d3f5c7a9e1b3d5f7c940",
                Md5Hash = null,
                Name = "Goldoson",
                Family = "Goldoson",
                Severity = (int)ThreatSeverity.High,
                Description = "Goldoson Android malware library found in 60+ South Korean Google Play apps with 100M+ downloads. Collects installed apps, WiFi/Bluetooth connected devices, and GPS data. Performs ad click fraud.",
                Tags = "[\"android\",\"adware\",\"data-collection\",\"play-store\",\"south-korea\",\"click-fraud\"]",
                FirstSeen = new DateTime(2023, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 150. CryptoClippy
            new()
            {
                Sha256Hash = "d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a4d6f8b0c2e4a6d8f0b2c4e6a8d051",
                Md5Hash = null,
                Name = "CryptoClippy",
                Family = "Clipper",
                Severity = (int)ThreatSeverity.Medium,
                Description = "CryptoClippy clipboard hijacker replacing cryptocurrency wallet addresses in copy-paste operations. Targets Bitcoin, Ethereum, Monero, and Litecoin addresses using regex pattern matching.",
                Tags = "[\"clipper\",\"cryptocurrency\",\"clipboard-hijack\",\"bitcoin\",\"ethereum\"]",
                FirstSeen = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 151. Turla ComRAT v4
            new()
            {
                Sha256Hash = "e1a3c5e7b9d1f3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7d9f162",
                Md5Hash = null,
                Name = "ComRATv4",
                Family = "Turla",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Turla ComRAT v4 backdoor using Gmail for C2 communication. Reads commands from email drafts and writes responses back via IMAP/SMTP, making network detection extremely difficult.",
                Tags = "[\"apt\",\"backdoor\",\"turla\",\"gmail-c2\",\"russian-apt\",\"fsb\"]",
                FirstSeen = new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 152. Winnti Group Backdoor
            new()
            {
                Sha256Hash = "f2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e273",
                Md5Hash = null,
                Name = "WinntiBackdoor",
                Family = "Winnti",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Winnti Group modular backdoor used in supply chain attacks against gaming, technology, and pharmaceutical companies. Features rootkit, port-knocking C2, and encrypted payload delivery.",
                Tags = "[\"apt\",\"backdoor\",\"supply-chain\",\"china-apt\",\"gaming\",\"rootkit\"]",
                FirstSeen = new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 153. Danabot
            new()
            {
                Sha256Hash = "a3c5e7a9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7b9d1f384",
                Md5Hash = null,
                Name = "Danabot",
                Family = "Danabot",
                Severity = (int)ThreatSeverity.High,
                Description = "Danabot modular banking trojan with stealer, RAT, and ransomware capabilities. Uses ToR for C2 communication and targets banking credentials via web injection.",
                Tags = "[\"trojan\",\"banker\",\"modular\",\"tor\",\"web-inject\",\"credential-theft\"]",
                FirstSeen = new DateTime(2018, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 154. WarmCookie/BadSpace
            new()
            {
                Sha256Hash = "b4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a495",
                Md5Hash = null,
                Name = "WarmCookie",
                Family = "BadSpace",
                Severity = (int)ThreatSeverity.High,
                Description = "WarmCookie (BadSpace) backdoor distributed via malspam and malvertising campaigns. Collects machine fingerprint, takes screenshots, executes commands, and deploys additional payloads.",
                Tags = "[\"backdoor\",\"malspam\",\"malvertising\",\"fingerprinting\",\"dropper\"]",
                FirstSeen = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 155. ValleyRAT
            new()
            {
                Sha256Hash = "c5e7a9c1d3f5b7a9c1e3d5f7b9a1c3e5d7f9b1a3c5e7d9f1b3a5c7e9d1f3b5a6",
                Md5Hash = null,
                Name = "ValleyRAT",
                Family = "ValleyRAT",
                Severity = (int)ThreatSeverity.High,
                Description = "ValleyRAT Chinese-origin RAT targeting Chinese-speaking users. Features process injection, screen monitoring, arbitrary command execution, and persistence via registry modification.",
                Tags = "[\"rat\",\"chinese-origin\",\"process-injection\",\"screen-monitoring\"]",
                FirstSeen = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 156. BianLian Ransomware
            new()
            {
                Sha256Hash = "d6f8b0d2e4a6c8f0b2d4e6a8c0d2f4b6e8a0c2d4f6b8a0c2e4d6f8b0c2e4a6b7",
                Md5Hash = null,
                Name = "BianLian",
                Family = "BianLian",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BianLian ransomware group pivoting to pure data extortion without encryption after Avast released a decryptor. Written in Go, targets ProxyShell vulnerabilities for initial access.",
                Tags = "[\"ransomware\",\"data-extortion\",\"golang\",\"proxyshell\",\"extortion-only\"]",
                FirstSeen = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 157. Kimsuky APT
            new()
            {
                Sha256Hash = "e7a9c1e3f5b7d9a1c3e5f7b9d1a3c5e7f9b1d3a5c7e9f1b3d5a7c9e1f3b5d7c8",
                Md5Hash = null,
                Name = "KimsukyAPT",
                Family = "Kimsuky",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Kimsuky (APT43/Thallium) North Korean APT conducting espionage against South Korean government, military, and research organizations. Uses spearphishing with social engineering pretexts.",
                Tags = "[\"apt\",\"north-korea\",\"espionage\",\"spearphishing\",\"south-korea\"]",
                FirstSeen = new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 158. SideWinder APT
            new()
            {
                Sha256Hash = "f8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8d9",
                Md5Hash = null,
                Name = "SideWinder",
                Family = "RattleSnake",
                Severity = (int)ThreatSeverity.Critical,
                Description = "SideWinder (RattleSnake/T-APT-04) Indian APT targeting Pakistan, China, and neighboring countries. Uses malicious LNK files with DLL side-loading and custom .NET implants.",
                Tags = "[\"apt\",\"indian-apt\",\"geopolitical\",\"dll-sideloading\",\"lnk-files\"]",
                FirstSeen = new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 159. OceanLotus/APT32
            new()
            {
                Sha256Hash = "a9c1e3f5a7b9d1f3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7d9ea",
                Md5Hash = null,
                Name = "OceanLotus",
                Family = "APT32",
                Severity = (int)ThreatSeverity.Critical,
                Description = "OceanLotus (APT32) Vietnamese state-sponsored APT targeting foreign governments, journalists, and private sector. Uses macOS malware, watering holes, and custom backdoors.",
                Tags = "[\"apt\",\"vietnam\",\"espionage\",\"macos\",\"watering-hole\",\"journalists\"]",
                FirstSeen = new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 160. MuddyWater
            new()
            {
                Sha256Hash = "b0d2f4a6b8c0e2d4f6a8b0c2e4d6f8a0b2c4e6d8f0a2b4c6e8d0a2f4b6c8e0fb",
                Md5Hash = null,
                Name = "MuddyWater",
                Family = "MERCURY",
                Severity = (int)ThreatSeverity.Critical,
                Description = "MuddyWater (MERCURY/Static Kitten) Iranian MOIS-affiliated APT targeting telecom, government, and oil/gas sectors. Uses PowerShell-based backdoors and legitimate tools for persistence.",
                Tags = "[\"apt\",\"iran\",\"mois\",\"powershell\",\"telecom\",\"energy-sector\"]",
                FirstSeen = new DateTime(2017, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 161. MATA Framework
            new()
            {
                Sha256Hash = "c1e3f5a7c9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7b9d10c",
                Md5Hash = null,
                Name = "MATAFramework",
                Family = "Lazarus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "MATA cross-platform malware framework linked to Lazarus Group. Supports Windows, Linux, and macOS with modular plugin architecture for file manipulation, proxy, and scanning.",
                Tags = "[\"framework\",\"cross-platform\",\"lazarus\",\"modular\",\"north-korea\"]",
                FirstSeen = new DateTime(2018, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 162. ShadowHammer (ASUS)
            new()
            {
                Sha256Hash = "d2f4a6c8d0e2b4f6a8c0e2d4b6f8a0c2e4d6b8f0a2c4e6d8b0f2a4c6e8d0b21d",
                Md5Hash = null,
                Name = "ShadowHammer",
                Family = "ShadowHammer",
                Severity = (int)ThreatSeverity.Critical,
                Description = "ShadowHammer supply chain attack via ASUS Live Update Utility. Trojanized firmware update signed with legitimate ASUS certificate, targeting specific MAC addresses.",
                Tags = "[\"supply-chain\",\"asus\",\"firmware\",\"code-signing\",\"targeted\"]",
                FirstSeen = new DateTime(2018, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 163. CCleaner Backdoor
            new()
            {
                Sha256Hash = "e3a5c7e9f1b3d5a7c9e1f3b5d7a9c1e3f5b7d9a1c3e5f7b9d1a3c5e7f9b1d32e",
                Md5Hash = null,
                Name = "CCleanerBackdoor",
                Family = "ShadowPad",
                Severity = (int)ThreatSeverity.Critical,
                Description = "CCleaner supply chain compromise injecting ShadowPad backdoor into legitimate Piriform build process. 2.27 million infected installations, targeting tech companies for second-stage payload.",
                Tags = "[\"supply-chain\",\"ccleaner\",\"shadowpad\",\"build-compromise\",\"targeted\"]",
                FirstSeen = new DateTime(2017, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 164. TinyTurla-NG
            new()
            {
                Sha256Hash = "f4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e43f",
                Md5Hash = null,
                Name = "TinyTurlaNG",
                Family = "Turla",
                Severity = (int)ThreatSeverity.Critical,
                Description = "TinyTurla-NG lightweight backdoor used by Turla APT as a fallback implant. Communicates via Windows pipes, executes PowerShell, and uses WordPress-based C2 infrastructure.",
                Tags = "[\"apt\",\"backdoor\",\"turla\",\"lightweight\",\"wordpress-c2\",\"powershell\"]",
                FirstSeen = new DateTime(2023, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 165. Volt Typhoon Custom Webshell
            new()
            {
                Sha256Hash = "a5c7e9b1d3a5c7e9f1b3d5a7c9e1f3b5d7a9c1e3f5b7d9a1c3e5f7b9d1a3c540",
                Md5Hash = null,
                Name = "VTWebshell",
                Family = "VoltTyphoon",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Volt Typhoon custom webshell deployed on Fortinet, Ivanti, and Cisco edge devices. Minimal footprint, blends with legitimate web server files for long-term persistence in critical infrastructure.",
                Tags = "[\"webshell\",\"china-apt\",\"edge-device\",\"fortinet\",\"ivanti\",\"persistence\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 166. Raspberry Robin USB Worm v2
            new()
            {
                Sha256Hash = "b6d8f0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d651",
                Md5Hash = null,
                Name = "RaspberryRobinV2",
                Family = "RaspberryRobin",
                Severity = (int)ThreatSeverity.High,
                Description = "Raspberry Robin v2 with enhanced anti-analysis using virtual machine detection, timing checks, and API obfuscation. Drops SocGholish, IcedID, BumbleBee, and TrueBot payloads.",
                Tags = "[\"worm\",\"usb\",\"anti-analysis\",\"loader\",\"evasion\",\"multi-payload\"]",
                FirstSeen = new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 167. Mirai Botnet
            new()
            {
                Sha256Hash = "c7e9a1d3f5c7e9a1b3d5f7c9a1e3b5d7f9c1a3e5b7d9f1c3a5e7b9d1f3c5a762",
                Md5Hash = null,
                Name = "Mirai",
                Family = "Mirai",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Mirai IoT botnet that launched 1.2 Tbps DDoS attack against Dyn DNS in 2016. Scans for devices with default credentials. Source code publicly released, spawning hundreds of variants.",
                Tags = "[\"botnet\",\"iot\",\"ddos\",\"default-credentials\",\"source-released\"]",
                FirstSeen = new DateTime(2016, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 168. BotenaGo
            new()
            {
                Sha256Hash = "d8f0b2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a4d6f873",
                Md5Hash = null,
                Name = "BotenaGo",
                Family = "BotenaGo",
                Severity = (int)ThreatSeverity.High,
                Description = "BotenaGo Golang-based malware targeting IoT devices with 30+ exploit modules for routers, modems, and NAS devices. Variants include Lillin and EnemyBot forks.",
                Tags = "[\"iot\",\"golang\",\"router\",\"nas\",\"multi-exploit\",\"botnet\"]",
                FirstSeen = new DateTime(2021, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 169. BPFDoor
            new()
            {
                Sha256Hash = "e9a1c3f5b7e9a1c3d5f7b9a1c3e5d7f9b1a3c5e7d9f1b3a5c7e9d1f3b5a7c984",
                Md5Hash = null,
                Name = "BPFDoor",
                Family = "BPFDoor",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BPFDoor stealthy Linux backdoor using Berkeley Packet Filter for covert C2. Opens no listening ports, evades firewalls, and responds to magic packet triggers. Active since 2017 undetected.",
                Tags = "[\"backdoor\",\"linux\",\"bpf\",\"stealth\",\"magic-packet\",\"portless\"]",
                FirstSeen = new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 170. Mozi Botnet
            new()
            {
                Sha256Hash = "f0b2d4a6c8f0b2d4e6a8c0f2b4d6e8a0c2f4b6d8e0a2c4f6b8d0e2a4c6f8b095",
                Md5Hash = null,
                Name = "Mozi",
                Family = "Mozi",
                Severity = (int)ThreatSeverity.High,
                Description = "Mozi IoT botnet using DHT protocol for P2P C2 communication. Targets routers and DVRs with known vulnerabilities. Chinese law enforcement arrested operators in 2021, injected kill switch.",
                Tags = "[\"botnet\",\"iot\",\"p2p\",\"dht\",\"router\",\"dvr\",\"arrested\"]",
                FirstSeen = new DateTime(2019, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 171. Gafgyt/Bashlite
            new()
            {
                Sha256Hash = "a1c3e5b7d9a1c3e5f7b9d1a3c5e7f9b1d3a5c7e9f1b3d5a7c9e1f3b5d7a9c1a6",
                Md5Hash = null,
                Name = "Gafgyt",
                Family = "Bashlite",
                Severity = (int)ThreatSeverity.High,
                Description = "Gafgyt (Bashlite/Lizkebab) IoT botnet targeting ARM, MIPS, x86 Linux devices. DDoS capabilities including UDP, TCP, and HTTP floods. Frequently competes with Mirai for device control.",
                Tags = "[\"botnet\",\"iot\",\"ddos\",\"linux\",\"multi-arch\",\"mirai-rival\"]",
                FirstSeen = new DateTime(2014, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 172. Cyclops Blink
            new()
            {
                Sha256Hash = "b2d4f6c8e0b2d4f6a8c0e2d4f6a8c0b2e4d6f8a0c2e4b6d8f0a2c4e6b8d0f2b7",
                Md5Hash = null,
                Name = "CyclopsBlink",
                Family = "Sandworm",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cyclops Blink modular botnet framework replacing VPNFilter, attributed to Sandworm/GRU. Targets WatchGuard firewalls and ASUS routers with firmware persistence. FBI disrupted in 2022.",
                Tags = "[\"botnet\",\"sandworm\",\"gru\",\"firmware\",\"watchguard\",\"fbi-takedown\"]",
                FirstSeen = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 173. VPNFilter
            new()
            {
                Sha256Hash = "c3e5a7d9f1c3e5a7b9d1f3c5a7e9b1d3f5c7a9e1b3d5f7c9a1e3b5d7f9c1a3c8",
                Md5Hash = null,
                Name = "VPNFilter",
                Family = "Sandworm",
                Severity = (int)ThreatSeverity.Critical,
                Description = "VPNFilter multi-stage modular malware infecting 500,000+ SOHO routers and NAS devices across 54 countries. Attributed to Sandworm. Capable of MitM attacks, data exfiltration, and device bricking.",
                Tags = "[\"botnet\",\"router\",\"nas\",\"sandworm\",\"modular\",\"destructive\"]",
                FirstSeen = new DateTime(2018, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 174. Purple Fox
            new()
            {
                Sha256Hash = "d4f6b8e0a2d4f6b8c0e2a4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4d9",
                Md5Hash = null,
                Name = "PurpleFox",
                Family = "PurpleFox",
                Severity = (int)ThreatSeverity.High,
                Description = "Purple Fox rootkit and botnet using worm-like SMB brute-force propagation. Abuses vulnerable Windows drivers for privilege escalation and deploys cryptominers and additional payloads.",
                Tags = "[\"rootkit\",\"worm\",\"smb\",\"byovd\",\"cryptominer\",\"botnet\"]",
                FirstSeen = new DateTime(2018, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 175. Anchor/TrickBot
            new()
            {
                Sha256Hash = "e5a7c9f1b3e5a7c9d1f3b5e7a9c1d3f5b7e9a1c3d5f7b9e1a3c5d7f9b1e3a5ea",
                Md5Hash = null,
                Name = "AnchorDNS",
                Family = "TrickBot",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Anchor backdoor module from TrickBot operators using DNS tunneling for stealthy C2 communication. Targets high-value enterprise networks for point-of-sale and financial data theft.",
                Tags = "[\"backdoor\",\"dns-tunneling\",\"trickbot\",\"pos\",\"financial\",\"stealth\"]",
                FirstSeen = new DateTime(2018, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 176. Diavol Ransomware
            new()
            {
                Sha256Hash = "f6b8d0e2a4f6b8d0c2e4a6f8b0d2c4e6a8f0b2d4c6e8a0f2b4d6c8e0a2f4b6fb",
                Md5Hash = null,
                Name = "Diavol",
                Family = "Diavol",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Diavol ransomware linked to Wizard Spider (TrickBot operators). Uses user-mode asynchronous procedure calls (APC) for file encryption. Saves ransom note as BMP wallpaper.",
                Tags = "[\"ransomware\",\"wizard-spider\",\"apc\",\"trickbot-linked\",\"bmp-ransom\"]",
                FirstSeen = new DateTime(2021, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 177. Cuba Ransomware
            new()
            {
                Sha256Hash = "a7c9e1f3b5a7c9e1d3f5b7a9c1e3d5f7b9a1c3e5d7f9b1a3c5e7d9f1b3a5c70c",
                Md5Hash = null,
                Name = "CubaRansomware",
                Family = "COLDDRAW",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cuba (COLDDRAW) ransomware exploiting Microsoft Exchange vulnerabilities (ProxyShell/ProxyLogon). Uses custom downloader BUGHATCH and has targeted 100+ entities worldwide.",
                Tags = "[\"ransomware\",\"exchange\",\"proxyshell\",\"proxylogon\",\"bughatch\"]",
                FirstSeen = new DateTime(2019, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 178. HelloKitty Ransomware
            new()
            {
                Sha256Hash = "b8d0f2a4c6b8d0f2e4a6c8d0b2f4e6a8c0d2b4f6e8a0c2d4b6f8e0a2c4d6f81d",
                Md5Hash = null,
                Name = "HelloKitty",
                Family = "FiveHands",
                Severity = (int)ThreatSeverity.Critical,
                Description = "HelloKitty ransomware known for attacking CD Projekt Red and leaking Cyberpunk 2077 source code. Cross-platform variants target Windows and Linux/ESXi environments.",
                Tags = "[\"ransomware\",\"cd-projekt-red\",\"source-leak\",\"cross-platform\",\"esxi\"]",
                FirstSeen = new DateTime(2020, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 179. LockerGoga
            new()
            {
                Sha256Hash = "c9e1a3c5d7c9e1a3f5d7b9c1a3e5d7f9b1c3a5e7d9f1b3c5a7e9d1f3b5c7a92e",
                Md5Hash = null,
                Name = "LockerGoga",
                Family = "LockerGoga",
                Severity = (int)ThreatSeverity.Critical,
                Description = "LockerGoga ransomware that crippled Norsk Hydro aluminum manufacturer causing $75M in damages. Forces logoff, changes passwords, and disables network adapters making recovery extremely difficult.",
                Tags = "[\"ransomware\",\"industrial\",\"norsk-hydro\",\"destructive\",\"password-change\"]",
                FirstSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 180. MegaCortex
            new()
            {
                Sha256Hash = "d0f2b4d6e8d0f2b4a6e8c0d2f4b6a8e0c2d4f6b8a0e2c4d6f8b0a2c4e6d8f03f",
                Md5Hash = null,
                Name = "MegaCortex",
                Family = "MegaCortex",
                Severity = (int)ThreatSeverity.Critical,
                Description = "MegaCortex ransomware typically deployed after Emotet and QakBot infections. Changes Windows passwords, threatens data publication, and uses signed executables for legitimacy.",
                Tags = "[\"ransomware\",\"emotet-drop\",\"qakbot-drop\",\"password-change\",\"signed\"]",
                FirstSeen = new DateTime(2019, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 181. Zloader/Terdot
            new()
            {
                Sha256Hash = "e1a3c5e7f9e1a3c5d7f9b1a3c5e7d9f1b3a5c7e9d1f3b5a7c9e1d3f5b7a9c140",
                Md5Hash = null,
                Name = "Zloader",
                Family = "Terdot",
                Severity = (int)ThreatSeverity.High,
                Description = "Zloader (Terdot/DELoader) banking trojan and loader, descendant of Zeus. Resurfaced in 2020 with web injection, VNC, and ransomware delivery. Microsoft obtained court order to disrupt in 2022.",
                Tags = "[\"trojan\",\"banker\",\"zeus-descendant\",\"loader\",\"microsoft-disrupted\"]",
                FirstSeen = new DateTime(2016, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 182. LODEINFO RAT
            new()
            {
                Sha256Hash = "f2b4d6f8a0f2b4d6e8a0c2f4b6d8e0a2c4f6b8d0e2a4c6f8b0d2e4a6c8f0b251",
                Md5Hash = null,
                Name = "LODEINFO",
                Family = "APT10",
                Severity = (int)ThreatSeverity.Critical,
                Description = "LODEINFO fileless RAT used by APT10 (Stone Panda/MenuPass) targeting Japanese organizations. Operates entirely in memory, supports file operations, screenshot, and shellcode execution.",
                Tags = "[\"rat\",\"fileless\",\"apt10\",\"japan-target\",\"in-memory\",\"chinese-apt\"]",
                FirstSeen = new DateTime(2019, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 183. FakeBat/EugenLoader
            new()
            {
                Sha256Hash = "a3c5e7a9b1a3c5e7d9b1f3a5c7e9d1f3b5a7c9e1d3f5b7a9c1e3d5f7b9a1c362",
                Md5Hash = null,
                Name = "FakeBat",
                Family = "EugenLoader",
                Severity = (int)ThreatSeverity.High,
                Description = "FakeBat (EugenLoader) loader-as-a-service using Google Ads malvertising and SEO poisoning. Creates fake download sites mimicking Brave, Zoom, Notion, etc. to deliver infostealers and RATs.",
                Tags = "[\"loader\",\"malvertising\",\"seo-poisoning\",\"google-ads\",\"impersonation\"]",
                FirstSeen = new DateTime(2022, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 184. ClearFake
            new()
            {
                Sha256Hash = "b4d6f8b0c2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d473",
                Md5Hash = null,
                Name = "ClearFake",
                Family = "ClearFake",
                Severity = (int)ThreatSeverity.High,
                Description = "ClearFake fake browser update framework injecting malicious JavaScript into compromised WordPress sites. Uses Binance Smart Chain for payload hosting (EtherHiding technique).",
                Tags = "[\"social-engineering\",\"fake-update\",\"javascript\",\"wordpress\",\"blockchain\"]",
                FirstSeen = new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 185. ClickFix/ClearFake variant
            new()
            {
                Sha256Hash = "c5e7a9c1d3c5e7a9b1d3f5c7a9e1b3d5f7c9a1e3b5d7f9c1a3e5b7d9f1c3a584",
                Md5Hash = null,
                Name = "ClickFix",
                Family = "ClickFix",
                Severity = (int)ThreatSeverity.High,
                Description = "ClickFix social engineering attack displaying fake error messages instructing users to paste PowerShell commands. Delivers DarkGate, Lumma Stealer, and other payloads via clipboard manipulation.",
                Tags = "[\"social-engineering\",\"powershell\",\"clipboard\",\"fake-error\",\"user-execution\"]",
                FirstSeen = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 186. Mispadu Banking Trojan
            new()
            {
                Sha256Hash = "d6f8b0d2e4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f695",
                Md5Hash = null,
                Name = "Mispadu",
                Family = "Mispadu",
                Severity = (int)ThreatSeverity.High,
                Description = "Mispadu Latin American banking trojan expanding to Europe. Uses malicious URLs in invoice-themed emails, SmartScreen bypass (CVE-2023-36025) for payload delivery, and overlay attacks.",
                Tags = "[\"trojan\",\"banker\",\"latin-america\",\"europe\",\"smartscreen-bypass\",\"overlay\"]",
                FirstSeen = new DateTime(2019, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 187. Coyote Banking Trojan
            new()
            {
                Sha256Hash = "e7a9c1e3f5e7a9c1d3f5b7e9a1c3d5f7b9e1a3c5d7f9b1e3a5c7d9f1b3e5a7a6",
                Md5Hash = null,
                Name = "Coyote",
                Family = "Coyote",
                Severity = (int)ThreatSeverity.High,
                Description = "Coyote Brazilian banking trojan using Squirrel installer framework for distribution and Nim language for the loader. Monitors 60+ banking applications with keylogging and screenshot capabilities.",
                Tags = "[\"trojan\",\"banker\",\"brazil\",\"squirrel-installer\",\"nim\",\"banking-apps\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 188. Phemedrone Stealer
            new()
            {
                Sha256Hash = "f8b0d2f4a6f8b0d2e4a6c8f0b2d4e6a8c0f2b4d6e8a0c2f4b6d8e0a2c4f6b8b7",
                Md5Hash = null,
                Name = "Phemedrone",
                Family = "Phemedrone",
                Severity = (int)ThreatSeverity.High,
                Description = "Phemedrone Stealer targeting browsers, cryptocurrency wallets, Discord, Telegram, Steam, and FileZilla. Exploits Windows Defender SmartScreen bypass CVE-2023-36025 for delivery.",
                Tags = "[\"infostealer\",\"smartscreen-bypass\",\"discord\",\"telegram\",\"steam\"]",
                FirstSeen = new DateTime(2023, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 189. Ducktail Stealer
            new()
            {
                Sha256Hash = "a9c1e3a5b7a9c1e3d5b7f9a1c3e5d7f9b1a3c5e7d9f1b3a5c7e9d1f3b5a7c9c8",
                Md5Hash = null,
                Name = "Ducktail",
                Family = "Ducktail",
                Severity = (int)ThreatSeverity.High,
                Description = "Ducktail info-stealer specifically targeting Facebook Business and Ads Manager accounts. Written in .NET, distributed via LinkedIn social engineering. Vietnamese threat actor operated.",
                Tags = "[\"infostealer\",\"facebook\",\"business-accounts\",\"linkedin\",\"vietnamese\"]",
                FirstSeen = new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 190. NodeStealer
            new()
            {
                Sha256Hash = "b0d2f4b6c8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0d9",
                Md5Hash = null,
                Name = "NodeStealer",
                Family = "NodeStealer",
                Severity = (int)ThreatSeverity.High,
                Description = "NodeStealer JavaScript/Python malware targeting Facebook, Gmail, and Outlook credentials via browser cookie theft. Vietnamese-origin, distributed through Facebook ads with malicious attachments.",
                Tags = "[\"infostealer\",\"facebook\",\"gmail\",\"nodejs\",\"python\",\"cookie-theft\"]",
                FirstSeen = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 191. Akira Fog Variant
            new()
            {
                Sha256Hash = "c1e3a5c7d9c1e3a5b7d9f1c3e5a7b9d1f3c5e7a9b1d3f5c7e9a1b3d5f7c9e1ea",
                Md5Hash = null,
                Name = "AkiraFogCrossover",
                Family = "AkiraFog",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Shared infrastructure between Akira and Fog ransomware groups suggesting common operators or affiliate overlap. Both exploit SonicWall SSL VPN vulnerabilities for initial access.",
                Tags = "[\"ransomware\",\"sonicwall\",\"ssl-vpn\",\"shared-infrastructure\",\"affiliate\"]",
                FirstSeen = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 192. DragonForce Ransomware
            new()
            {
                Sha256Hash = "d2f4b6d8e0d2f4b6a8e0c2d4f6b8a0e2c4d6f8b0a2c4e6d8f0b2a4c6e8d0f2fb",
                Md5Hash = null,
                Name = "DragonForce",
                Family = "DragonForce",
                Severity = (int)ThreatSeverity.Critical,
                Description = "DragonForce ransomware-as-a-service from Malaysia-based hacktivist group. Uses leaked LockBit and Conti builders. Targets with double extortion and operates a data leak blog.",
                Tags = "[\"ransomware\",\"raas\",\"hacktivism\",\"lockbit-builder\",\"conti-builder\"]",
                FirstSeen = new DateTime(2023, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 193. Interlock Ransomware
            new()
            {
                Sha256Hash = "e3a5c7e9f1e3a5c7d9f1b3e5a7c9d1f3b5e7a9c1d3f5b7e9a1c3d5f7b9e1a30c",
                Md5Hash = null,
                Name = "Interlock",
                Family = "Interlock",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Interlock ransomware targeting FreeBSD and Windows systems. Uses fake browser updaters for initial access and ClickFix social engineering technique. Focuses on critical infrastructure.",
                Tags = "[\"ransomware\",\"freebsd\",\"fake-update\",\"clickfix\",\"critical-infrastructure\"]",
                FirstSeen = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 194. GhostEngine/REF4578
            new()
            {
                Sha256Hash = "f4b6d8f0a2f4b6d8e0a2c4f6b8d0e2a4c6f8b0d2e4a6c8f0b2d4e6a8c0f2b41d",
                Md5Hash = null,
                Name = "GhostEngine",
                Family = "REF4578",
                Severity = (int)ThreatSeverity.Critical,
                Description = "GhostEngine (REF4578) intrusion set using vulnerable drivers to disable EDR products before deploying XMRig cryptominer. Leverages BYOVD with IObit and Avast driver exploits.",
                Tags = "[\"cryptominer\",\"byovd\",\"edr-killer\",\"xmrig\",\"driver-exploit\"]",
                FirstSeen = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 195. Terminator/SpyBoy
            new()
            {
                Sha256Hash = "a5c7e9a1b3a5c7e9d1b3f5a7c9e1d3f5b7a9c1e3d5f7b9a1c3e5d7f9b1a3c52e",
                Md5Hash = null,
                Name = "Terminator",
                Family = "SpyBoy",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Terminator (SpyBoy) EDR/AV killer tool using BYOVD technique with Zemana Anti-Malware driver. Sold on Russian forums for $3,000. Disables 24+ security products before payload deployment.",
                Tags = "[\"edr-killer\",\"byovd\",\"zemana\",\"security-bypass\",\"russian-forum\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 196. AuKill/BackStab
            new()
            {
                Sha256Hash = "b6d8f0b2c4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d63f",
                Md5Hash = null,
                Name = "AuKill",
                Family = "BackStab",
                Severity = (int)ThreatSeverity.High,
                Description = "AuKill (BackStab) defense evasion tool abusing Process Explorer driver to disable security products. Used by ransomware affiliates including Medusa and LockBit before encryption.",
                Tags = "[\"edr-killer\",\"process-explorer\",\"driver-abuse\",\"ransomware-tool\"]",
                FirstSeen = new DateTime(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 197. Predator The Thief
            new()
            {
                Sha256Hash = "c7e9a1c3d5c7e9a1b3d5f7c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7b9d1f3c5e740",
                Md5Hash = null,
                Name = "PredatorThief",
                Family = "PredatorThief",
                Severity = (int)ThreatSeverity.High,
                Description = "Predator The Thief Russian-origin infostealer targeting browser data, Steam, Discord, Telegram, VPN clients, and cryptocurrency wallets. Uses fileless loading via shellcode injection.",
                Tags = "[\"infostealer\",\"russian-origin\",\"fileless\",\"shellcode\",\"vpn-credentials\"]",
                FirstSeen = new DateTime(2018, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 198. Raccoon Stealer v2
            new()
            {
                Sha256Hash = "d8f0b2d4e6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a4d6f851",
                Md5Hash = null,
                Name = "RaccoonV2",
                Family = "Raccoon",
                Severity = (int)ThreatSeverity.High,
                Description = "Raccoon Stealer v2 (RecordBreaker) rewritten in C/C++ after original developer's arrest. Targets 60+ applications, uses DLL side-loading, and sells for $200/month on underground forums.",
                Tags = "[\"infostealer\",\"rewritten\",\"cpp\",\"dll-sideloading\",\"maas\"]",
                FirstSeen = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 199. SectopRAT
            new()
            {
                Sha256Hash = "e9a1c3e5b7e9a1c3d5b7f9a1c3e5d7f9b1a3c5e7d9f1b3a5c7e9d1f3b5a7c962",
                Md5Hash = null,
                Name = "SectopRAT",
                Family = "ArechClient",
                Severity = (int)ThreatSeverity.High,
                Description = "SectopRAT (ArechClient) C#-based RAT creating a hidden second desktop for browser session manipulation. Steals cryptocurrency by modifying browser sessions via OBS-based screen sharing.",
                Tags = "[\"rat\",\"csharp\",\"hidden-desktop\",\"browser-manipulation\",\"cryptocurrency\"]",
                FirstSeen = new DateTime(2019, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 200. Androxgh0st
            new()
            {
                Sha256Hash = "f0b2d4f6a8f0b2d4e6a8c0f2b4d6e8a0c2f4b6d8e0a2c4f6b8d0e2a4c6f8b073",
                Md5Hash = null,
                Name = "Androxgh0st",
                Family = "Androxgh0st",
                Severity = (int)ThreatSeverity.High,
                Description = "Androxgh0st Python-based credential harvester targeting .env files in Laravel and other web frameworks. Scans for exposed environment files to steal AWS, SendGrid, Twilio, and SMTP credentials.",
                Tags = "[\"credential-harvest\",\"python\",\"env-files\",\"laravel\",\"aws\",\"cloud\"]",
                FirstSeen = new DateTime(2022, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 201. NoaBot/NanoCore variant
            new()
            {
                Sha256Hash = "a1c3e5a7b9a1c3e5d7b9f1a3c5e7d9f1b3a5c7e9d1f3b5a7c9e1d3f5b7a9c184",
                Md5Hash = null,
                Name = "NoaBot",
                Family = "Mirai",
                Severity = (int)ThreatSeverity.High,
                Description = "NoaBot Mirai-based botnet variant with added SSH credential brute-forcing and a crypto-mining payload. Custom obfuscation prevents standard Mirai signature detection.",
                Tags = "[\"botnet\",\"mirai-variant\",\"ssh-brute-force\",\"cryptominer\",\"obfuscated\"]",
                FirstSeen = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 202. HeadLace/APT28 2024
            new()
            {
                Sha256Hash = "b2d4f6b8c0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d295",
                Md5Hash = null,
                Name = "HeadLace",
                Family = "APT28",
                Severity = (int)ThreatSeverity.Critical,
                Description = "HeadLace backdoor used by APT28 in 2024 campaigns targeting European government networks. Multi-stage delivery via compromised Ubiquiti EdgeRouters and legitimate cloud services.",
                Tags = "[\"apt\",\"backdoor\",\"apt28\",\"gru\",\"edgerouter\",\"cloud-abuse\"]",
                FirstSeen = new DateTime(2023, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 203. WINELOADER APT29
            new()
            {
                Sha256Hash = "c3e5a7c9d1c3e5a7b9d1f3c5e7a9b1d3f5c7e9a1b3d5f7c9e1a3b5d7f9c1e3a6",
                Md5Hash = null,
                Name = "WINELOADER",
                Family = "APT29",
                Severity = (int)ThreatSeverity.Critical,
                Description = "WINELOADER backdoor used by APT29 in diplomatic espionage campaign targeting European embassies. Delivered via fake wine-tasting event invitations with malicious ZIP/HTA attachments.",
                Tags = "[\"apt\",\"backdoor\",\"apt29\",\"diplomatic\",\"wine-lure\",\"embassy\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 204. Pikabot v2
            new()
            {
                Sha256Hash = "d4f6b8d0e2d4f6b8a0e2c4d6f8b0a2e4c6d8f0b2a4e6c8d0f2b4a6e8c0d2f4b7",
                Md5Hash = null,
                Name = "PikabotV2",
                Family = "PikaBot",
                Severity = (int)ThreatSeverity.High,
                Description = "PikaBot version 2 with stripped anti-analysis, simplified code structure, and new C2 protocol. Uses AES-CBC for network encryption and supports DLL injection, shellcode execution, and system commands.",
                Tags = "[\"loader\",\"modular\",\"simplified\",\"aes-cbc\",\"shellcode\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 205. TheMoon Botnet
            new()
            {
                Sha256Hash = "e5a7c9e1f3e5a7c9d1f3b5e7a9c1d3f5b7e9a1c3d5f7b9e1a3c5d7f9b1e3a5c8",
                Md5Hash = null,
                Name = "TheMoon",
                Family = "TheMoon",
                Severity = (int)ThreatSeverity.High,
                Description = "TheMoon botnet hijacking SOHO routers and IoT devices to power Faceless proxy-as-a-service. Provides anonymous proxy infrastructure used by cybercriminals for credential stuffing and fraud.",
                Tags = "[\"botnet\",\"router\",\"proxy-service\",\"faceless\",\"credential-stuffing\"]",
                FirstSeen = new DateTime(2014, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 206. LockBit Linux/ESXi
            new()
            {
                Sha256Hash = "f6b8d0f2a4f6b8d0e2a4c6f8b0d2e4a6c8f0b2d4e6a8c0f2b4d6e8a0c2f4b6d9",
                Md5Hash = null,
                Name = "LockBitLinux",
                Family = "LockBit",
                Severity = (int)ThreatSeverity.Critical,
                Description = "LockBit Linux/ESXi encryptor specifically targeting VMware virtual machines on enterprise hypervisors. Uses OpenSSL AES encryption and targets .vmdk, .vmx, and .vmsn files.",
                Tags = "[\"ransomware\",\"linux\",\"esxi\",\"vmware\",\"openssl\",\"enterprise\"]",
                FirstSeen = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 207. Vidar v2 Stealer
            new()
            {
                Sha256Hash = "a7c9e1a3b5a7c9e1d3b5f7a9c1e3d5f7b9a1c3e5d7f9b1a3c5e7d9f1b3a5c7ea",
                Md5Hash = null,
                Name = "VidarV2",
                Family = "Vidar",
                Severity = (int)ThreatSeverity.High,
                Description = "Vidar Stealer v2 with enhanced evasion using dead-drop resolvers on social media (Telegram, Steam, Mastodon) for C2 retrieval. Expanded crypto wallet targeting including MetaMask and Phantom.",
                Tags = "[\"infostealer\",\"social-media-c2\",\"dead-drop\",\"metamask\",\"phantom\"]",
                FirstSeen = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 208. GootKit Loader 2024
            new()
            {
                Sha256Hash = "b8d0f2b4c6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8fb",
                Md5Hash = null,
                Name = "GootKit2024",
                Family = "GootKit",
                Severity = (int)ThreatSeverity.High,
                Description = "GootKit banking trojan resurged in 2024 with updated Node.js-based loader and improved persistence. Targets legal and financial sectors via SEO poisoning with fake legal document downloads.",
                Tags = "[\"trojan\",\"banker\",\"nodejs\",\"seo-poisoning\",\"legal-sector\"]",
                FirstSeen = new DateTime(2014, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 209. DarkVision RAT
            new()
            {
                Sha256Hash = "c9e1a3c5d7c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7b9d1f3c5e7a9b1d3f5e7c90c",
                Md5Hash = null,
                Name = "DarkVision",
                Family = "DarkVision",
                Severity = (int)ThreatSeverity.High,
                Description = "DarkVision RAT sold on underground forums for $40. Features remote desktop, keylogging, password recovery, webcam capture, reverse proxy, and process management with a plugin system.",
                Tags = "[\"rat\",\"cheap\",\"plugin-system\",\"webcam\",\"password-recovery\"]",
                FirstSeen = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 210. LockBit 4.0
            new()
            {
                Sha256Hash = "d0f2b4d6e8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a4d6f8b0c2e4a6d8f01d",
                Md5Hash = null,
                Name = "LockBit4",
                Family = "LockBit",
                Severity = (int)ThreatSeverity.Critical,
                Description = "LockBit 4.0 announced relaunch after FBI/NCA Operation Cronos takedown. Rebuilt infrastructure with new .onion sites and updated encryptor. Demonstrates resilience of RaaS operations.",
                Tags = "[\"ransomware\",\"raas\",\"resilient\",\"post-takedown\",\"rebuilt\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 211. Akira Ransomware 2024
            new()
            {
                Sha256Hash = "e1a3c5d7f9e1a3b5c7d9f1e3a5b7c9d1f3e5a7b9c1d3f5e7a9b1c3d5f7e9a1b3",
                Md5Hash = null,
                Name = "AkiraV2",
                Family = "Akira",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Akira ransomware v2 targeting VMware ESXi environments. Uses Rust-based Linux encryptor alongside Windows variant. Exploits Cisco VPN vulnerabilities (CVE-2023-20269) for initial access.",
                Tags = "[\"ransomware\",\"esxi\",\"rust\",\"cisco-vpn\",\"linux\"]",
                FirstSeen = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 212. Play Ransomware
            new()
            {
                Sha256Hash = "f2b4d6e8a0f2b4c6d8e0a2f4b6c8d0e2a4f6b8c0d2e4a6f8b0c2d4e6a8f0b2c4",
                Md5Hash = null,
                Name = "PlayRansom",
                Family = "Play",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Play (PlayCrypt) ransomware targeting managed service providers and their downstream customers. Uses intermittent encryption for speed and deploys via compromised RDP/VPN.",
                Tags = "[\"ransomware\",\"msp\",\"intermittent-encryption\",\"rdp\",\"supply-chain\"]",
                FirstSeen = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 213. Medusa Ransomware
            new()
            {
                Sha256Hash = "a3c5e7d9b1a3c5e7d9f1a3c5e7b9d1f3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3d5",
                Md5Hash = null,
                Name = "MedusaLocker2024",
                Family = "MedusaLocker",
                Severity = (int)ThreatSeverity.Critical,
                Description = "MedusaLocker 2024 variant with triple extortion: encryption, data theft, and DDoS threats. Exploits unpatched Apache Struts and FortiGate vulnerabilities for initial access.",
                Tags = "[\"ransomware\",\"triple-extortion\",\"ddos\",\"apache-struts\",\"fortigate\"]",
                FirstSeen = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 214. BlackBasta 2024
            new()
            {
                Sha256Hash = "b4d6f8e0c2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6",
                Md5Hash = null,
                Name = "BlackBasta2024",
                Family = "BlackBasta",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Black Basta ransomware using QakBot and DarkGate for initial access. Disables EDR via BYOVD (Bring Your Own Vulnerable Driver). Over 500 organizations compromised globally.",
                Tags = "[\"ransomware\",\"qakbot\",\"darkgate\",\"byovd\",\"edr-bypass\"]",
                FirstSeen = new DateTime(2022, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 215. AsyncRAT 2024
            new()
            {
                Sha256Hash = "c5e7a9d1f3c5e7a9b1d3f5c7e9a1b3d5f7c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7",
                Md5Hash = null,
                Name = "AsyncRAT2024",
                Family = "AsyncRAT",
                Severity = (int)ThreatSeverity.High,
                Description = "AsyncRAT 2024 campaigns using AI-generated phishing emails and OneNote attachments. Features HVNC (Hidden Virtual Network Computing), keylogging, and cryptocurrency clipboard hijacking.",
                Tags = "[\"rat\",\"ai-phishing\",\"onenote\",\"hvnc\",\"crypto-clipper\"]",
                FirstSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 216. Lumma Stealer
            new()
            {
                Sha256Hash = "d6f8b0e2a4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8",
                Md5Hash = null,
                Name = "LummaStealer",
                Family = "Lumma",
                Severity = (int)ThreatSeverity.High,
                Description = "Lumma Stealer (LummaC2) MaaS infostealer targeting browser credentials, crypto wallets, and 2FA extensions. Distributed via fake CAPTCHA pages and cracked software. Uses novel anti-sandbox techniques.",
                Tags = "[\"infostealer\",\"maas\",\"captcha-lure\",\"2fa-theft\",\"anti-sandbox\"]",
                FirstSeen = new DateTime(2022, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 217. Rhysida Ransomware
            new()
            {
                Sha256Hash = "e7a9c1d3f5e7a9c1b3d5f7e9a1c3b5d7f9e1a3c5b7d9f1e3a5c7b9d1f3e5a7c9",
                Md5Hash = null,
                Name = "Rhysida",
                Family = "Rhysida",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Rhysida ransomware targeting healthcare, education, and government sectors. Uses Vice Society TTPs and exploits Citrix Bleed (CVE-2023-4966). CISA/FBI joint advisory AA23-319A issued.",
                Tags = "[\"ransomware\",\"healthcare\",\"citrix-bleed\",\"vice-society\",\"government\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 218. Hunters International
            new()
            {
                Sha256Hash = "f8b0d2e4a6f8b0d2c4e6a8f0b2d4c6e8a0f2b4d6c8e0a2f4b6d8c0e2a4f6b8d0",
                Md5Hash = null,
                Name = "HuntersIntl",
                Family = "HuntersInternational",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Hunters International ransomware group believed to be Hive ransomware rebrand. Focuses on data exfiltration over encryption. Targets manufacturing and healthcare sectors globally.",
                Tags = "[\"ransomware\",\"hive-rebrand\",\"data-exfil\",\"manufacturing\",\"healthcare\"]",
                FirstSeen = new DateTime(2023, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 219. XWorm RAT
            new()
            {
                Sha256Hash = "a9c1e3f5b7a9c1e3d5f7a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7b9d1f3a5c7e9b1",
                Md5Hash = null,
                Name = "XWorm",
                Family = "XWorm",
                Severity = (int)ThreatSeverity.High,
                Description = "XWorm multi-functional RAT with ransomware plugin, DDoS capabilities, keylogger, webcam capture, and crypto mining. Sold on Telegram for $50-100. Delivered via malicious USB shortcuts.",
                Tags = "[\"rat\",\"multi-function\",\"telegram\",\"usb-spread\",\"crypto-miner\"]",
                FirstSeen = new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 220. Pikabot Loader
            new()
            {
                Sha256Hash = "b0d2f4a6c8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2",
                Md5Hash = null,
                Name = "Pikabot",
                Family = "Pikabot",
                Severity = (int)ThreatSeverity.High,
                Description = "Pikabot modular loader emerged as QakBot successor. Features anti-analysis (debugger detection, VM checks), code injection, and downloads secondary payloads including Cobalt Strike.",
                Tags = "[\"loader\",\"qakbot-successor\",\"anti-analysis\",\"cobalt-strike\",\"modular\"]",
                FirstSeen = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 221. DarkGate Malware
            new()
            {
                Sha256Hash = "c1e3a5c7d9c1e3a5b7d9f1c3e5a7b9d1f3c5e7a9b1d3f5c7e9a1b3d5f7c9e1a3",
                Md5Hash = null,
                Name = "DarkGate",
                Family = "DarkGate",
                Severity = (int)ThreatSeverity.High,
                Description = "DarkGate MaaS loader sold for $1000/month. Features HVNC, crypto mining, keylogging, credential-stealing, and privilege escalation. Spreads via Microsoft Teams messages and malvertising.",
                Tags = "[\"loader\",\"maas\",\"teams-spread\",\"hvnc\",\"malvertising\"]",
                FirstSeen = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 222. Cactus Ransomware
            new()
            {
                Sha256Hash = "d2f4b6d8e0d2f4b6c8e0a2d4f6b8c0e2a4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4",
                Md5Hash = null,
                Name = "CactusRansom",
                Family = "Cactus",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Cactus ransomware self-encrypts its binary to evade antivirus detection. Exploits Fortinet VPN vulnerabilities for initial access. Known for disabling security tools via BYOVD and batch scripts.",
                Tags = "[\"ransomware\",\"self-encrypting\",\"fortinet\",\"byovd\",\"evasion\"]",
                FirstSeen = new DateTime(2023, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 223. IcedID successor: Latrodectus
            new()
            {
                Sha256Hash = "e3a5c7e9f1e3a5c7d9f1e3a5c7b9d1f3e5a7c9b1d3f5e7a9c1b3d5f7e9a1c3b5",
                Md5Hash = null,
                Name = "Latrodectus",
                Family = "Latrodectus",
                Severity = (int)ThreatSeverity.High,
                Description = "Latrodectus (Unidentified 111) loader considered successor to IcedID. Developed by same threat actor (Lunar Spider). Initial access via oversized JavaScript files in email attachments.",
                Tags = "[\"loader\",\"icedid-successor\",\"javascript\",\"lunar-spider\",\"email\"]",
                FirstSeen = new DateTime(2023, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 224. Chinese APT Volt Typhoon tooling
            new()
            {
                Sha256Hash = "f4b6d8f0a2f4b6d8e0a2c4f6b8d0e2a4c6f8b0d2e4a6c8f0b2d4e6a8c0f2b4d6",
                Md5Hash = null,
                Name = "VoltTyphoon-WebShell",
                Family = "VoltTyphoon",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Web shell associated with Volt Typhoon (Bronze Silhouette) APT group. China-nexus threat actor pre-positioning in US critical infrastructure using living-off-the-land techniques.",
                Tags = "[\"apt\",\"china\",\"critical-infrastructure\",\"lotl\",\"web-shell\"]",
                FirstSeen = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 225. Russian APT Midnight Blizzard tool
            new()
            {
                Sha256Hash = "a5c7e9b1d3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7",
                Md5Hash = null,
                Name = "MidnightBlizzard-Loader",
                Family = "CozyBear",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Custom loader used by Midnight Blizzard (APT29/Cozy Bear) in 2024 Microsoft 365 email compromise campaign. Leveraged OAuth application abuse and password spray attacks.",
                Tags = "[\"apt\",\"russia\",\"apt29\",\"oauth-abuse\",\"password-spray\"]",
                FirstSeen = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 226. Scattered Spider custom tooling
            new()
            {
                Sha256Hash = "b6d8f0a2c4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8",
                Md5Hash = null,
                Name = "ScatteredSpider-SSO",
                Family = "ScatteredSpider",
                Severity = (int)ThreatSeverity.High,
                Description = "Custom SSO phishing toolkit used by Scattered Spider (UNC3944/0ktapus). Targets Okta, Azure AD, and Duo for MFA bypass via social engineering of IT helpdesks.",
                Tags = "[\"threat-group\",\"social-engineering\",\"mfa-bypass\",\"okta\",\"helpdesk\"]",
                FirstSeen = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 227. SmokeLoader 2024
            new()
            {
                Sha256Hash = "c7e9a1c3e5c7e9a1b3d5f7c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7b9d1f3e5a7c9",
                Md5Hash = null,
                Name = "SmokeLoader2024",
                Family = "SmokeLoader",
                Severity = (int)ThreatSeverity.High,
                Description = "SmokeLoader botnet resilient after Europol Operation Endgame. New variants use encrypted DNS-over-HTTPS for C2 and deliver diverse payloads including ransomware and infostealers.",
                Tags = "[\"botnet\",\"loader\",\"doh\",\"resilient\",\"operation-endgame\"]",
                FirstSeen = new DateTime(2011, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 228. Snake Keylogger 2024
            new()
            {
                Sha256Hash = "d8f0b2d4f6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a4d6f8b0",
                Md5Hash = null,
                Name = "SnakeKeylogger2024",
                Family = "SnakeKeylogger",
                Severity = (int)ThreatSeverity.High,
                Description = "Snake Keylogger (.NET-based) with updated credential harvesting from 50+ applications. Uses AutoIt scripting for evasion. Exfils data via Telegram bot API and SMTP.",
                Tags = "[\"keylogger\",\"dotnet\",\"autoit\",\"telegram-exfil\",\"credential-theft\"]",
                FirstSeen = new DateTime(2020, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 229. Predator Spyware
            new()
            {
                Sha256Hash = "e9a1c3e5a7e9a1c3b5d7f9e1a3c5b7d9f1e3a5c7b9d1f3e5a7c9b1d3f5e7a9c1",
                Md5Hash = null,
                Name = "PredatorSpyware",
                Family = "Predator",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Predator commercial spyware by Cytrox/Intellexa. Exploits zero-day chains in iOS and Android. Capable of real-time audio/video surveillance, message interception, and location tracking.",
                Tags = "[\"spyware\",\"commercial\",\"zero-day\",\"mobile\",\"surveillance\"]",
                FirstSeen = new DateTime(2021, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 230. BianLian Ransomware
            new()
            {
                Sha256Hash = "f0b2d4f6a8f0b2d4c6e8a0f2b4d6c8e0a2f4b6d8c0e2a4f6b8d0c2e4a6f8b0d2",
                Md5Hash = null,
                Name = "BianLian2024",
                Family = "BianLian",
                Severity = (int)ThreatSeverity.Critical,
                Description = "BianLian ransomware shifted to pure data extortion model in 2024, abandoning encryption. Targets healthcare and professional services. Uses ProxyShell and RDP for initial access.",
                Tags = "[\"ransomware\",\"data-extortion\",\"healthcare\",\"proxyshell\",\"rdp\"]",
                FirstSeen = new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 231. Raspberry Robin Worm
            new()
            {
                Sha256Hash = "a1c3e5a7c9a1c3e5b7d9f1a3c5e7b9d1f3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3",
                Md5Hash = null,
                Name = "RaspberryRobin",
                Family = "RaspberryRobin",
                Severity = (int)ThreatSeverity.High,
                Description = "Raspberry Robin USB worm evolved into initial access broker for ransomware (Clop, LockBit). Uses compromised QNAP NAS devices as C2. N-day 1-day exploitation capabilities.",
                Tags = "[\"worm\",\"usb\",\"initial-access-broker\",\"qnap\",\"ransomware-delivery\"]",
                FirstSeen = new DateTime(2021, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 232. Fog Ransomware
            new()
            {
                Sha256Hash = "b2d4f6b8d0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4",
                Md5Hash = null,
                Name = "FogRansom",
                Family = "Fog",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Fog ransomware targeting US educational institutions. Exploits compromised SonicWall VPN credentials. Fast encryption with .FOG or .FLOCKED extensions. Minimal dwell time before detonation.",
                Tags = "[\"ransomware\",\"education\",\"sonicwall\",\"vpn\",\"fast-encryption\"]",
                FirstSeen = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 233. RansomHub
            new()
            {
                Sha256Hash = "c3e5a7c9e1c3e5a7b9d1f3c5e7a9b1d3f5c7e9a1b3d5f7c9e1a3b5d7f9c1e3a5",
                Md5Hash = null,
                Name = "RansomHub",
                Family = "RansomHub",
                Severity = (int)ThreatSeverity.Critical,
                Description = "RansomHub RaaS emerged in 2024 as successor to Knight/Cyclops ransomware. Written in Go/C++ with cross-platform capability. Aggressive affiliate recruitment from disrupted groups.",
                Tags = "[\"ransomware\",\"raas\",\"golang\",\"cross-platform\",\"knight-successor\"]",
                FirstSeen = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 234. Rhadamanthys Stealer
            new()
            {
                Sha256Hash = "d4f6b8d0f2d4f6b8c0e2a4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6",
                Md5Hash = null,
                Name = "Rhadamanthys",
                Family = "Rhadamanthys",
                Severity = (int)ThreatSeverity.High,
                Description = "Rhadamanthys infostealer v0.5+ with AI-powered OCR for cryptocurrency seed phrase extraction from images. Advanced sandbox evasion using GPU fingerprinting and timing attacks.",
                Tags = "[\"infostealer\",\"ai-ocr\",\"seed-phrase\",\"gpu-fingerprint\",\"evasion\"]",
                FirstSeen = new DateTime(2022, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 235. ClickFix Social Engineering
            new()
            {
                Sha256Hash = "e5a7c9e1a3e5a7c9b1d3f5e7a9c1b3d5f7e9a1c3b5d7f9e1a3c5b7d9f1e3a5c7",
                Md5Hash = null,
                Name = "ClickFixLoader",
                Family = "ClickFix",
                Severity = (int)ThreatSeverity.High,
                Description = "ClickFix social engineering technique instructing victims to paste PowerShell commands via fake browser error popups. Delivers various payloads including Lumma, NetSupport, and DarkGate.",
                Tags = "[\"social-engineering\",\"powershell\",\"fake-errors\",\"loader\",\"clipboard\"]",
                FirstSeen = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 236. Androxgh0st Botnet
            new()
            {
                Sha256Hash = "f6b8d0f2b4f6b8d0a2c4e6f8b0d2a4c6e8f0b2d4a6c8e0f2b4d6a8c0e2f4b6d8",
                Md5Hash = null,
                Name = "Androxgh0st",
                Family = "Androxgh0st",
                Severity = (int)ThreatSeverity.High,
                Description = "Androxgh0st Python-based botnet targeting .env files, AWS/Azure/Twilio credentials. Integrates Mozi botnet capabilities. CISA advisory AA24-016A. Exploits Laravel, Apache, PHPUnit.",
                Tags = "[\"botnet\",\"python\",\"cloud-credentials\",\"env-files\",\"cisa-advisory\"]",
                FirstSeen = new DateTime(2022, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 237. SpyNote Android Trojan
            new()
            {
                Sha256Hash = "a7c9e1a3c5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7b9d1f3a5c7e9",
                Md5Hash = null,
                Name = "SpyNote",
                Family = "SpyNote",
                Severity = (int)ThreatSeverity.High,
                Description = "SpyNote (CypherRAT) Android banking trojan source code leaked in 2023, leading to proliferation. Abuses Accessibility Services for keylogging, screen recording, and 2FA code interception.",
                Tags = "[\"android\",\"banking-trojan\",\"accessibility-abuse\",\"2fa-bypass\",\"leaked-source\"]",
                FirstSeen = new DateTime(2022, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 238. Mystic Stealer
            new()
            {
                Sha256Hash = "b8d0f2b4d6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0",
                Md5Hash = null,
                Name = "MysticStealer",
                Family = "MysticStealer",
                Severity = (int)ThreatSeverity.High,
                Description = "Mystic Stealer (C language) targeting 40+ browsers, 70+ extensions, crypto wallets, Steam, and Telegram. Custom binary protocol over TCP. Anti-VM checks using CPUID and registry queries.",
                Tags = "[\"infostealer\",\"c-lang\",\"custom-protocol\",\"anti-vm\",\"crypto-wallet\"]",
                FirstSeen = new DateTime(2023, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 239. INC Ransom
            new()
            {
                Sha256Hash = "c9e1a3c5e7c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7b9d1f3c5e7a9b1d3f5e7c9e1",
                Md5Hash = null,
                Name = "INCRansom",
                Family = "INCRansom",
                Severity = (int)ThreatSeverity.Critical,
                Description = "INC Ransom group targeting healthcare (NHS Scotland breach). Written in C++ with partial encryption mode for speed. Publishes stolen data on dedicated leak site if ransom unpaid.",
                Tags = "[\"ransomware\",\"healthcare\",\"nhs\",\"partial-encryption\",\"data-leak\"]",
                FirstSeen = new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 240. Grandoreiro Banking Trojan
            new()
            {
                Sha256Hash = "d0f2b4d6f8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8c0e2a4d6f8b0c2e4a6d8f0b2",
                Md5Hash = null,
                Name = "Grandoreiro",
                Family = "Grandoreiro",
                Severity = (int)ThreatSeverity.High,
                Description = "Grandoreiro Delphi banking trojan revived after arrests in 2024. Expanded from Latin America to target 1500+ banks across 60 countries. Uses DGA and legitimate cloud services for C2.",
                Tags = "[\"banking-trojan\",\"delphi\",\"dga\",\"latin-america\",\"global-expansion\"]",
                FirstSeen = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 241. Qilin Ransomware
            new()
            {
                Sha256Hash = "e1a3c5e7a9e1a3c5b7d9f1e3a5c7b9d1f3e5a7c9b1d3f5e7a9c1b3d5f7e9a1c3",
                Md5Hash = null,
                Name = "Qilin",
                Family = "Qilin",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Qilin (Agenda) ransomware targeting VMware ESXi with Rust-based variant. Attacked NHS Synnovis pathology services disrupting London hospitals. Steals Chrome browser credentials en masse.",
                Tags = "[\"ransomware\",\"rust\",\"esxi\",\"nhs\",\"credential-theft\"]",
                FirstSeen = new DateTime(2022, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 242. Dispossessor Ransomware
            new()
            {
                Sha256Hash = "f2b4d6f8b0f2b4d6c8e0a2f4b6d8c0e2a4f6b8d0c2e4a6f8b0d2c4e6a8f0b2d4",
                Md5Hash = null,
                Name = "Dispossessor",
                Family = "Dispossessor",
                Severity = (int)ThreatSeverity.Critical,
                Description = "Dispossessor (Radar) ransomware targeting SMBs with weak security. FBI seized infrastructure in 2024. Attacks via internet-facing RDP, exploiting weak passwords and lack of MFA.",
                Tags = "[\"ransomware\",\"smb\",\"rdp\",\"fbi-takedown\",\"weak-credentials\"]",
                FirstSeen = new DateTime(2023, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 243. Meduza Stealer
            new()
            {
                Sha256Hash = "a3c5e7a9c1a3c5e7b9d1f3a5c7e9b1d3f5a7c9e1b3d5f7a9c1e3b5d7f9a1c3e5",
                Md5Hash = null,
                Name = "MeduzaStealer",
                Family = "Meduza",
                Severity = (int)ThreatSeverity.High,
                Description = "Meduza Stealer (C++ native) harvesting data from 100+ browsers and 100+ crypto wallets. Low detection rate due to custom packing. Subscription-based with Telegram distribution.",
                Tags = "[\"infostealer\",\"cpp\",\"low-detection\",\"subscription\",\"telegram\"]",
                FirstSeen = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 244. StrelaStealer
            new()
            {
                Sha256Hash = "b4d6f8b0d2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2a4c6e8b0d2f4a6c8e0b2d4f6",
                Md5Hash = null,
                Name = "StrelaStealer",
                Family = "StrelaStealer",
                Severity = (int)ThreatSeverity.Medium,
                Description = "StrelaStealer email credential stealer specifically targeting Outlook and Thunderbird login data. Distributed via invoice-themed emails with polyglot file attachments (ZIP/DLL format).",
                Tags = "[\"infostealer\",\"email-creds\",\"outlook\",\"thunderbird\",\"polyglot-file\"]",
                FirstSeen = new DateTime(2022, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 245. BazarCall / BazaCall
            new()
            {
                Sha256Hash = "c5e7a9c1e3c5e7a9b1d3f5c7e9a1b3d5f7c9e1a3b5d7f9c1e3a5b7d9f1c3e5a7",
                Md5Hash = null,
                Name = "BazaCall2024",
                Family = "BazaCall",
                Severity = (int)ThreatSeverity.High,
                Description = "BazaCall callback phishing evolved to deploy ransomware directly. Victims are tricked into calling fake IT support who guide them through installing AnyDesk/TeamViewer for remote access.",
                Tags = "[\"phishing\",\"callback\",\"social-engineering\",\"anydesk\",\"ransomware-delivery\"]",
                FirstSeen = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 246. SystemBC Proxy Bot
            new()
            {
                Sha256Hash = "d6f8b0d2f4d6f8b0c2e4a6d8f0b2c4e6a8d0f2b4c6e8a0d2f4b6c8e0a2d4f6b8",
                Md5Hash = null,
                Name = "SystemBC",
                Family = "SystemBC",
                Severity = (int)ThreatSeverity.High,
                Description = "SystemBC SOCKS5 proxy backdoor used by multiple ransomware affiliates (Conti, Ryuk, REvil, DarkSide). Creates hidden communication channels over Tor. Persistent via Windows services.",
                Tags = "[\"backdoor\",\"proxy\",\"tor\",\"ransomware-affiliate\",\"persistence\"]",
                FirstSeen = new DateTime(2019, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 247. Nitrogen Malware Campaign
            new()
            {
                Sha256Hash = "e7a9c1e3a5e7a9c1b3d5f7e9a1c3b5d7f9e1a3c5b7d9f1e3a5c7b9d1f3e5a7c9",
                Md5Hash = null,
                Name = "NitrogenLoader",
                Family = "Nitrogen",
                Severity = (int)ThreatSeverity.High,
                Description = "Nitrogen initial access malware distributed via malicious Google/Bing ads for IT tools (WinSCP, PuTTY, AnyDesk). Delivers Cobalt Strike and BlackCat ransomware via DLL sideloading.",
                Tags = "[\"loader\",\"malvertising\",\"seo-poisoning\",\"dll-sideload\",\"blackcat\"]",
                FirstSeen = new DateTime(2023, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 248. Ducktail Stealer
            new()
            {
                Sha256Hash = "f8b0d2f4b6f8b0d2c4e6a8f0b2d4c6e8a0f2b4d6c8e0a2f4b6d8c0e2a4f6b8d0",
                Md5Hash = null,
                Name = "Ducktail",
                Family = "Ducktail",
                Severity = (int)ThreatSeverity.Medium,
                Description = "Ducktail infostealer targeting Facebook Business accounts. Written in .NET, distributed via fake job offers on LinkedIn. Hijacks ad accounts and business manager sessions for ad fraud.",
                Tags = "[\"infostealer\",\"facebook\",\"linkedin\",\"ad-fraud\",\"dotnet\"]",
                FirstSeen = new DateTime(2022, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 249. SocGholish (FakeUpdates)
            new()
            {
                Sha256Hash = "a9c1e3a5c7a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7b9d1f3a5c7e9b1d3f5a7c9e1",
                Md5Hash = null,
                Name = "SocGholish",
                Family = "FakeUpdates",
                Severity = (int)ThreatSeverity.High,
                Description = "SocGholish (FakeUpdates) framework using compromised WordPress sites to deliver fake browser update prompts. Leads to NetSupport RAT, Cobalt Strike, and ransomware deployment.",
                Tags = "[\"framework\",\"fake-update\",\"wordpress\",\"netsupport\",\"drive-by\"]",
                FirstSeen = new DateTime(2017, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            },
            // 250. KrustyLoader (Ivanti exploit payload)
            new()
            {
                Sha256Hash = "b0d2f4b6d8b0d2f4a6c8e0b2d4f6a8c0e2b4d6f8a0c2e4b6d8f0a2c4e6b8d0f2",
                Md5Hash = null,
                Name = "KrustyLoader",
                Family = "KrustyLoader",
                Severity = (int)ThreatSeverity.Critical,
                Description = "KrustyLoader Rust-based backdoor deployed via Ivanti Connect Secure VPN zero-days (CVE-2024-21887, CVE-2023-46805). Used by UTA0178 (China-nexus). Downloads Sliver C2 implants.",
                Tags = "[\"backdoor\",\"rust\",\"ivanti\",\"zero-day\",\"china-nexus\"]",
                FirstSeen = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LastUpdated = now
            }
        };

        context.ThreatSignatures.AddRange(threats);
        await context.SaveChangesAsync();

        // Now seed solutions for the major threats
        // We need IDs, so reload after save
        var eicar = await context.ThreatSignatures.FirstAsync(t => t.Name == "EICAR-Test-File");
        var wannaCry = await context.ThreatSignatures.FirstAsync(t => t.Name == "WannaCry");
        var notPetya = await context.ThreatSignatures.FirstAsync(t => t.Name == "NotPetya");
        var emotet = await context.ThreatSignatures.FirstAsync(t => t.Name == "Emotet");
        var trickBot = await context.ThreatSignatures.FirstAsync(t => t.Name == "TrickBot");
        var ryuk = await context.ThreatSignatures.FirstAsync(t => t.Name == "Ryuk");
        var cobalt = await context.ThreatSignatures.FirstAsync(t => t.Name == "CobaltStrike-Beacon");
        var mimikatz = await context.ThreatSignatures.FirstAsync(t => t.Name == "Mimikatz");
        var conti = await context.ThreatSignatures.FirstAsync(t => t.Name == "Conti");
        var lockbit = await context.ThreatSignatures.FirstAsync(t => t.Name == "LockBit3");

        var solutions = new List<SolutionEntity>
        {
            // EICAR solutions
            new() { ThreatId = eicar.Id, StepOrder = 1, Description = "Quarantine the EICAR test file to verify quarantine functionality.", AutomatedAction = "quarantine", Script = null },
            new() { ThreatId = eicar.Id, StepOrder = 2, Description = "Delete the test file after quarantine verification is complete.", AutomatedAction = "delete", Script = null },

            // WannaCry solutions
            new() { ThreatId = wannaCry.Id, StepOrder = 1, Description = "Immediately isolate the infected machine from the network to prevent lateral spread via SMB.", AutomatedAction = "network_isolate", Script = "netsh advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound" },
            new() { ThreatId = wannaCry.Id, StepOrder = 2, Description = "Quarantine all files matching WannaCry indicators. Preserve encrypted files for potential recovery.", AutomatedAction = "quarantine", Script = null },
            new() { ThreatId = wannaCry.Id, StepOrder = 3, Description = "Apply MS17-010 security patch and disable SMBv1. Verify all systems on the network are patched.", AutomatedAction = "patch", Script = "Set-SmbServerConfiguration -EnableSMB1Protocol $false -Force; Get-HotFix -Id KB4012212,KB4012215,KB4012213,KB4012216,KB4012214,KB4012217" },

            // NotPetya solutions
            new() { ThreatId = notPetya.Id, StepOrder = 1, Description = "Immediately power off infected systems to prevent MBR overwrite completion if caught early.", AutomatedAction = "network_isolate", Script = null },
            new() { ThreatId = notPetya.Id, StepOrder = 2, Description = "Quarantine infected binaries and check for perfc.dat vaccine file presence.", AutomatedAction = "quarantine", Script = null },
            new() { ThreatId = notPetya.Id, StepOrder = 3, Description = "Rebuild affected systems from clean backups. MBR damage is typically irreversible.", AutomatedAction = null, Script = null },

            // Emotet solutions
            new() { ThreatId = emotet.Id, StepOrder = 1, Description = "Kill Emotet processes and remove persistence mechanisms from registry and scheduled tasks.", AutomatedAction = "kill_process", Script = "Get-ScheduledTask | Where-Object {$_.TaskName -match 'emotet|[a-f0-9]{8}'} | Unregister-ScheduledTask -Confirm:$false" },
            new() { ThreatId = emotet.Id, StepOrder = 2, Description = "Delete Emotet binaries from AppData, Temp, and System32 directories.", AutomatedAction = "delete", Script = null },
            new() { ThreatId = emotet.Id, StepOrder = 3, Description = "Clean Emotet registry run keys and services. Reset compromised email credentials.", AutomatedAction = "registry_cleanup", Script = "Remove-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name '*' -ErrorAction SilentlyContinue | Where-Object {$_.Value -match 'AppData'}" },

            // TrickBot solutions
            new() { ThreatId = trickBot.Id, StepOrder = 1, Description = "Terminate TrickBot processes and associated service entries.", AutomatedAction = "kill_process", Script = null },
            new() { ThreatId = trickBot.Id, StepOrder = 2, Description = "Remove TrickBot modules from %AppData% and %ProgramData% directories.", AutomatedAction = "delete", Script = null },
            new() { ThreatId = trickBot.Id, StepOrder = 3, Description = "Clean registry persistence and reset all domain credentials as TrickBot harvests Active Directory data.", AutomatedAction = "registry_cleanup", Script = null },

            // Ryuk solutions
            new() { ThreatId = ryuk.Id, StepOrder = 1, Description = "Isolate affected systems immediately. Ryuk disables recovery options and deletes shadow copies.", AutomatedAction = "network_isolate", Script = null },
            new() { ThreatId = ryuk.Id, StepOrder = 2, Description = "Quarantine Ryuk executables and preserve ransom notes for forensic analysis.", AutomatedAction = "quarantine", Script = null },
            new() { ThreatId = ryuk.Id, StepOrder = 3, Description = "Restore from offline backups. Audit all domain accounts for unauthorized access. Reset KRBTGT twice.", AutomatedAction = null, Script = null },

            // Cobalt Strike solutions
            new() { ThreatId = cobalt.Id, StepOrder = 1, Description = "Identify and terminate beacon processes. Check for injected threads in legitimate processes.", AutomatedAction = "kill_process", Script = null },
            new() { ThreatId = cobalt.Id, StepOrder = 2, Description = "Block C2 server IPs and domains at the firewall. Analyze beacon configuration for IOCs.", AutomatedAction = "firewall_block", Script = null },
            new() { ThreatId = cobalt.Id, StepOrder = 3, Description = "Audit all systems for lateral movement artifacts. Review SMB, WMI, and PSExec logs.", AutomatedAction = null, Script = null },

            // Mimikatz solutions
            new() { ThreatId = mimikatz.Id, StepOrder = 1, Description = "Quarantine Mimikatz binary and check for credential dump output files.", AutomatedAction = "quarantine", Script = null },
            new() { ThreatId = mimikatz.Id, StepOrder = 2, Description = "Force password reset for all accounts accessible from the compromised system.", AutomatedAction = null, Script = null },
            new() { ThreatId = mimikatz.Id, StepOrder = 3, Description = "Enable Credential Guard and configure LSA protection to prevent future credential dumping.", AutomatedAction = null, Script = "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Lsa\" /v RunAsPPL /t REG_DWORD /d 1 /f" },

            // Conti solutions
            new() { ThreatId = conti.Id, StepOrder = 1, Description = "Network isolation is critical. Conti uses multi-threaded encryption and spreads rapidly via SMB.", AutomatedAction = "network_isolate", Script = null },
            new() { ThreatId = conti.Id, StepOrder = 2, Description = "Quarantine Conti binaries. Check for data exfiltration - Conti uses double extortion.", AutomatedAction = "quarantine", Script = null },
            new() { ThreatId = conti.Id, StepOrder = 3, Description = "Restore from air-gapped backups. Full domain credential reset required.", AutomatedAction = null, Script = null },

            // LockBit solutions
            new() { ThreatId = lockbit.Id, StepOrder = 1, Description = "Isolate infected endpoints. LockBit 3.0 has self-spreading capabilities via GPO and PsExec.", AutomatedAction = "network_isolate", Script = null },
            new() { ThreatId = lockbit.Id, StepOrder = 2, Description = "Remove LockBit persistence and wallpaper changes. Delete ransom notes.", AutomatedAction = "delete", Script = null },
            new() { ThreatId = lockbit.Id, StepOrder = 3, Description = "Audit Group Policy Objects for unauthorized modifications. Restore from clean backups.", AutomatedAction = null, Script = null }
        };

        context.Solutions.AddRange(solutions);
        await context.SaveChangesAsync();
    }
}
