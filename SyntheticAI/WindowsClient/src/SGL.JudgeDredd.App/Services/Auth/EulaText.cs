namespace SGL.JudgeDredd.App.Services.Auth;

public static class EulaText
{
    public const string FullText = """
        END-USER LICENSE AGREEMENT (EULA)
        SGL-AI (SyntheticAI) Security Software
        Version 1.0

        IMPORTANT - READ CAREFULLY BEFORE INSTALLING OR USING THIS SOFTWARE

        This End-User License Agreement ("EULA") is a legal agreement between you ("User")
        and Synthetic Game Labs ("Developer", "We", "Us") for the SGL-AI (SyntheticAI)
        security software application ("Software").

        BY INSTALLING, COPYING, OR OTHERWISE USING THIS SOFTWARE, YOU AGREE TO BE BOUND
        BY THE TERMS OF THIS EULA. IF YOU DO NOT AGREE, DO NOT INSTALL OR USE THE SOFTWARE.

        1. GRANT OF LICENSE
        We grant you a non-exclusive, non-transferable, limited license to install and use
        the Software on a single device for personal or organizational security purposes.

        2. SOFTWARE DESCRIPTION AND WARNINGS
        This Software includes:
        - AI-powered antivirus scanning and threat detection
        - Firewall management and network protection
        - Security monitoring (process, registry, network, USB)
        - Local AI language model for security analysis
        - Remote assistance capabilities for authorized administrators

        IMPORTANT WARNINGS:

        a) SYSTEM ACCESS: This Software requires administrator privileges and will:
           - Access and scan all files on your system
           - Monitor running processes and network connections
           - Modify Windows Firewall rules
           - Monitor registry changes
           - Access Task Manager and system information
           - Quarantine or delete files identified as threats

        b) AI ANALYSIS: The AI component provides probabilistic analysis. It is NOT
           infallible. False positives (safe files flagged as threats) and false negatives
           (threats missed) may occur. This Software should NOT be your sole security solution.

        c) FILE MODIFICATION: The Software may quarantine, move, or delete files it identifies
           as malicious. This action may be irreversible. We recommend maintaining backups.

        d) NETWORK MONITORING: The Software monitors network connections to detect threats.
           This includes logging IP addresses, ports, and connection metadata.

        e) REMOTE ASSISTANCE: If you request remote assistance from an administrator,
           they will be able to view your screen. This feature requires your explicit consent
           at the time of each session.

        f) DATA COLLECTION: Account information (username, email) is stored locally and
           transmitted to the administrator for account management purposes only.

        g) AI MODEL: The Software runs a local AI model that requires significant system
           resources (8-16GB RAM). Performance may vary based on your hardware.

        3. LIMITATION OF LIABILITY
        TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW:

        a) THE SOFTWARE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
           IMPLIED, INCLUDING BUT NOT LIMITED TO WARRANTIES OF MERCHANTABILITY, FITNESS
           FOR A PARTICULAR PURPOSE, AND NON-INFRINGEMENT.

        b) THE DEVELOPER SHALL NOT BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
           SPECIAL, CONSEQUENTIAL, OR EXEMPLARY DAMAGES, INCLUDING BUT NOT LIMITED TO:
           - Loss of data or files
           - System damage or corruption
           - Loss of business or revenue
           - Security breaches that the Software fails to prevent
           - Damage caused by false positive detections
           - Any damage resulting from quarantine or deletion of files
           - System performance degradation
           - Network connectivity issues caused by firewall rules

        c) YOU ACKNOWLEDGE THAT NO SECURITY SOFTWARE CAN GUARANTEE 100% PROTECTION
           AGAINST ALL THREATS. USE OF THIS SOFTWARE IS AT YOUR OWN RISK.

        4. USER RESPONSIBILITIES
        a) Maintain regular backups of important data
        b) Do not rely solely on this Software for security protection
        c) Keep your operating system and other software updated
        d) Report bugs and issues to the developer
        e) Do not reverse engineer, decompile, or disassemble the Software
        f) Do not use the Software for illegal purposes

        5. ACCOUNT TERMS
        a) You must provide accurate registration information
        b) You are responsible for maintaining the confidentiality of your password
        c) Account sharing is not permitted
        d) We reserve the right to deactivate accounts that violate these terms

        6. TERMINATION
        This license is effective until terminated. We may terminate your license if you
        fail to comply with any term of this EULA. Upon termination, you must destroy all
        copies of the Software.

        7. GOVERNING LAW
        This EULA shall be governed by the laws of the jurisdiction in which the Developer
        operates, without regard to conflict of law principles.

        8. CONTACT
        For support, inquiries, or to report issues:
        Email: syntheticgamelabs@gmail.com

        9. ACKNOWLEDGMENT
        BY CLICKING "I AGREE" OR INSTALLING THE SOFTWARE, YOU ACKNOWLEDGE THAT YOU HAVE
        READ THIS EULA, UNDERSTAND IT, AND AGREE TO BE BOUND BY ITS TERMS AND CONDITIONS.

        Copyright (C) 2025 Synthetic Game Labs. All rights reserved.
        """;
}
