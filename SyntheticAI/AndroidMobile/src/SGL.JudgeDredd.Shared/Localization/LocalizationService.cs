using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;

namespace SGL.JudgeDredd.Shared.Localization;

/// <summary>
/// Runtime localization service that provides translated strings for all UI elements.
/// Supports: English (en), Spanish (es), Afrikaans (af), Russian (ru), French (fr), Chinese Mandarin (zh).
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private string _currentLanguage = "en";
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<string>? LanguageChanged;

    /// <summary>Languages supported with their display names and flag codes.</summary>
    public static readonly LanguageInfo[] SupportedLanguages =
    [
        new("en", "English",    "us"),
        new("es", "Castellano", "es"),
        new("af", "Afrikaans",  "za"),
        new("ru", "Русский",    "ru"),
        new("fr", "Français",   "fr"),
        new("zh", "中文",       "cn"),
    ];

    private LocalizationService()
    {
        LoadBuiltInTranslations();
    }

    /// <summary>Current language code (e.g. "en", "es").</summary>
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            _currentLanguage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke(this, value);
        }
    }

    /// <summary>Gets a localized string by key. Falls back to English if not found.</summary>
    public string this[string key]
    {
        get
        {
            if (_translations.TryGetValue(_currentLanguage, out var langDict)
                && langDict.TryGetValue(key, out var value))
                return value;

            if (_currentLanguage != "en"
                && _translations.TryGetValue("en", out var enDict)
                && enDict.TryGetValue(key, out var enValue))
                return enValue;

            return key;
        }
    }

    /// <summary>Gets a formatted localized string.</summary>
    public string Format(string key, params object[] args)
    {
        var template = this[key];
        try { return string.Format(template, args); }
        catch { return template; }
    }

    /// <summary>Sets the current language by code.</summary>
    public void SetLanguage(string langCode)
    {
        if (SupportedLanguages.Any(l => l.Code == langCode))
            CurrentLanguage = langCode;
    }

    /// <summary>Loads additional translations from a JSON file.</summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            if (data == null) return;

            foreach (var (lang, strings) in data)
            {
                if (!_translations.ContainsKey(lang))
                    _translations[lang] = new Dictionary<string, string>();

                foreach (var (k, v) in strings)
                    _translations[lang][k] = v;
            }
        }
        catch { }
    }

    private void LoadBuiltInTranslations()
    {
        // ── English (default) ──
        _translations["en"] = new Dictionary<string, string>
        {
            // Navigation
            ["nav.dashboard"] = "Dashboard",
            ["nav.scan"] = "Scan",
            ["nav.firewall"] = "Firewall",
            ["nav.security"] = "Security",
            ["nav.network"] = "Network",
            ["nav.privacy"] = "Privacy",
            ["nav.email"] = "Email Scan",
            ["nav.quarantine"] = "Quarantine",
            ["nav.schedule"] = "Schedule",
            ["nav.fim"] = "FIM",
            ["nav.monitor"] = "Monitor",
            ["nav.threats"] = "Threats",
            ["nav.updates"] = "Updates",
            ["nav.processes"] = "Processes",
            ["nav.tools"] = "Tools",
            ["nav.share"] = "Share",
            ["nav.faq"] = "FAQ",
            ["nav.chat"] = "Chat",
            ["nav.settings"] = "Settings",
            ["nav.help"] = "Help",
            ["nav.admin"] = "Admin",
            ["nav.uieditor"] = "UI Editor",

            // Dashboard
            ["dashboard.title"] = "Security Dashboard",
            ["dashboard.protection"] = "Protection Status",
            ["dashboard.active"] = "Active",
            ["dashboard.inactive"] = "Inactive",
            ["dashboard.threatlevel"] = "Threat Level",
            ["dashboard.lastscan"] = "Last Scan",
            ["dashboard.totalthreats"] = "Total Threats",
            ["dashboard.quickscan"] = "Quick Scan",
            ["dashboard.fullscan"] = "Full Scan",
            ["dashboard.connectedclients"] = "Connected Clients",

            // Scan
            ["scan.title"] = "Antivirus Scan",
            ["scan.quick"] = "Quick Scan",
            ["scan.extended"] = "Extended Scan",
            ["scan.custom"] = "Custom Scan",
            ["scan.start"] = "Start Scan",
            ["scan.stop"] = "Stop Scan",
            ["scan.scanning"] = "Scanning...",
            ["scan.complete"] = "Scan Complete",
            ["scan.threatsFound"] = "Threats Found",
            ["scan.filesScanned"] = "Files Scanned",
            ["scan.noThreats"] = "No threats detected",

            // Firewall
            ["firewall.title"] = "Firewall Management",
            ["firewall.rules"] = "Active Rules",
            ["firewall.addRule"] = "Add Rule",
            ["firewall.removeRule"] = "Remove Rule",
            ["firewall.enabled"] = "Firewall Enabled",
            ["firewall.disabled"] = "Firewall Disabled",

            // Chat
            ["chat.title"] = "AI Chat Assistant",
            ["chat.placeholder"] = "Type your message...",
            ["chat.send"] = "Send",
            ["chat.clear"] = "Clear Chat",

            // Settings
            ["settings.title"] = "Settings",
            ["settings.general"] = "General",
            ["settings.scanner"] = "Scanner",
            ["settings.firewallSettings"] = "Firewall",
            ["settings.llm"] = "LLM Configuration",
            ["settings.security"] = "Security",
            ["settings.save"] = "Save Settings",
            ["settings.language"] = "Language",
            ["settings.startWithWindows"] = "Start with Windows",
            ["settings.minimizeToTray"] = "Minimize to Tray",
            ["settings.realTimeProtection"] = "Real-time Protection",

            // Admin
            ["admin.title"] = "Admin Panel",
            ["admin.users"] = "User Management",
            ["admin.reports"] = "Reports",
            ["admin.diagnostics"] = "Diagnostics",
            ["admin.hardware"] = "Hardware Monitor",
            ["admin.llmslots"] = "LLM Engines",
            ["admin.backup"] = "Knowledge Backup",
            ["admin.banUser"] = "Ban User",
            ["admin.deleteUser"] = "Delete User",
            ["admin.promoteAdmin"] = "Promote to Admin",

            // Hardware Monitor
            ["hw.cpuUsage"] = "CPU Usage",
            ["hw.ramUsage"] = "RAM Usage",
            ["hw.gpuUsage"] = "GPU Usage",
            ["hw.temperature"] = "Temperature",
            ["hw.fanSpeed"] = "Fan Speed",
            ["hw.diskUsage"] = "Disk Usage",

            // LLM Slots
            ["llm.slot"] = "Slot",
            ["llm.mount"] = "Mount",
            ["llm.unmount"] = "Unmount",
            ["llm.mounted"] = "Mounted",
            ["llm.unmounted"] = "Unmounted",
            ["llm.loading"] = "Loading...",
            ["llm.mainEngine"] = "Main Engine",
            ["llm.promptCleanup"] = "Prompt Cleanup",
            ["llm.ttsEngine"] = "TTS Engine",
            ["llm.aiChat"] = "AI Chat",
            ["llm.selectModel"] = "Select Model",
            ["llm.selectRole"] = "Select Role",
            ["llm.selectAbility"] = "Select Ability",

            // Abilities
            ["ability.agent"] = "Agent",
            ["ability.monitor"] = "Monitor",
            ["ability.dualPower"] = "Dual Power Main",
            ["ability.imageGen"] = "Image Generation",
            ["ability.tts"] = "Text to Speech",
            ["ability.promptClean"] = "Prompt Clean Up",

            // Common
            ["common.ok"] = "OK",
            ["common.cancel"] = "Cancel",
            ["common.save"] = "Save",
            ["common.delete"] = "Delete",
            ["common.refresh"] = "Refresh",
            ["common.close"] = "Close",
            ["common.yes"] = "Yes",
            ["common.no"] = "No",
            ["common.error"] = "Error",
            ["common.success"] = "Success",
            ["common.loading"] = "Loading...",
            ["common.search"] = "Search",
            ["common.status"] = "Status",
            ["common.name"] = "Name",
            ["common.actions"] = "Actions",

            // Login
            ["login.title"] = "Login",
            ["login.username"] = "Username",
            ["login.password"] = "Password",
            ["login.signin"] = "Sign In",
            ["login.register"] = "Register",
            ["login.forgotPassword"] = "Forgot Password?",

            // Protection messages
            ["msg.allSystemsOk"] = "All systems operational.",
            ["msg.rtpDisabled"] = "Real-time protection is disabled.",
            ["msg.welcome"] = "Welcome, {0}. All systems operational.",
            ["msg.serverDown"] = "Server is unreachable.",
            ["msg.exitWarning"] = "If you exit SGL SyntheticAI, your system will be vulnerable and unprotected.",

            // Firewall (view-specific)
            ["firewall.quickPresets"] = "Quick Presets",
            ["firewall.gamingMode"] = "Gaming Mode",
            ["firewall.streamingMode"] = "Streaming Mode",
            ["firewall.lockdownMode"] = "Lockdown Mode",
            ["firewall.activeConnections"] = "Active Connections",
            ["firewall.direction"] = "Direction",
            ["firewall.protocol"] = "Protocol",
            ["firewall.localPorts"] = "Local Ports",
            ["firewall.remoteAddr"] = "Remote Addr",

            // Chat (view-specific)
            ["chat.loadModel"] = "Load Model",
            ["chat.showThinking"] = "Show thinking",
            ["chat.imageGen"] = "AI Image Generation",
            ["chat.prompt"] = "Prompt",
            ["chat.generate"] = "Generate",

            // Quarantine
            ["quarantine.title"] = "Quarantine Vault",
            ["quarantine.clearAll"] = "Clear All",
            ["quarantine.restore"] = "Restore",

            // Security Monitor
            ["security.title"] = "Security Monitor",
            ["security.activeMonitors"] = "Active Monitors",
            ["security.alerts"] = "Security Alerts",
            ["security.acknowledge"] = "Acknowledge",
            ["security.dismiss"] = "Dismiss",

            // Common extras
            ["common.download"] = "Download",
            ["common.enabled"] = "Enabled",
            ["common.disabled"] = "Disabled",
        };

        // ── Spanish (Castellano) ──
        _translations["es"] = new Dictionary<string, string>
        {
            ["nav.dashboard"] = "Panel",
            ["nav.scan"] = "Escanear",
            ["nav.firewall"] = "Cortafuegos",
            ["nav.security"] = "Seguridad",
            ["nav.network"] = "Red",
            ["nav.privacy"] = "Privacidad",
            ["nav.email"] = "Correo",
            ["nav.quarantine"] = "Cuarentena",
            ["nav.schedule"] = "Programar",
            ["nav.fim"] = "FIM",
            ["nav.monitor"] = "Monitor",
            ["nav.threats"] = "Amenazas",
            ["nav.updates"] = "Actualizaciones",
            ["nav.processes"] = "Procesos",
            ["nav.tools"] = "Herramientas",
            ["nav.share"] = "Compartir",
            ["nav.faq"] = "FAQ",
            ["nav.chat"] = "Chat",
            ["nav.settings"] = "Ajustes",
            ["nav.help"] = "Ayuda",
            ["nav.admin"] = "Admin",
            ["nav.uieditor"] = "Editor UI",

            ["dashboard.title"] = "Panel de Seguridad",
            ["dashboard.protection"] = "Estado de Protección",
            ["dashboard.active"] = "Activo",
            ["dashboard.inactive"] = "Inactivo",
            ["dashboard.threatlevel"] = "Nivel de Amenaza",
            ["dashboard.lastscan"] = "Último Escaneo",
            ["dashboard.totalthreats"] = "Amenazas Totales",
            ["dashboard.quickscan"] = "Escaneo Rápido",
            ["dashboard.fullscan"] = "Escaneo Completo",
            ["dashboard.connectedclients"] = "Clientes Conectados",

            ["scan.title"] = "Escaneo Antivirus",
            ["scan.quick"] = "Escaneo Rápido",
            ["scan.extended"] = "Escaneo Extendido",
            ["scan.custom"] = "Escaneo Personalizado",
            ["scan.start"] = "Iniciar Escaneo",
            ["scan.stop"] = "Detener Escaneo",
            ["scan.scanning"] = "Escaneando...",
            ["scan.complete"] = "Escaneo Completo",
            ["scan.threatsFound"] = "Amenazas Encontradas",
            ["scan.filesScanned"] = "Archivos Escaneados",
            ["scan.noThreats"] = "No se detectaron amenazas",

            ["firewall.title"] = "Gestión del Cortafuegos",
            ["firewall.rules"] = "Reglas Activas",
            ["firewall.addRule"] = "Añadir Regla",
            ["firewall.removeRule"] = "Eliminar Regla",
            ["firewall.enabled"] = "Cortafuegos Activo",
            ["firewall.disabled"] = "Cortafuegos Inactivo",

            ["chat.title"] = "Asistente de Chat IA",
            ["chat.placeholder"] = "Escribe tu mensaje...",
            ["chat.send"] = "Enviar",
            ["chat.clear"] = "Borrar Chat",

            ["settings.title"] = "Ajustes",
            ["settings.general"] = "General",
            ["settings.scanner"] = "Escáner",
            ["settings.firewallSettings"] = "Cortafuegos",
            ["settings.llm"] = "Configuración LLM",
            ["settings.security"] = "Seguridad",
            ["settings.save"] = "Guardar Ajustes",
            ["settings.language"] = "Idioma",
            ["settings.startWithWindows"] = "Iniciar con Windows",
            ["settings.minimizeToTray"] = "Minimizar a Bandeja",
            ["settings.realTimeProtection"] = "Protección en Tiempo Real",

            ["admin.title"] = "Panel de Administración",
            ["admin.users"] = "Gestión de Usuarios",
            ["admin.reports"] = "Informes",
            ["admin.diagnostics"] = "Diagnósticos",
            ["admin.hardware"] = "Monitor de Hardware",
            ["admin.llmslots"] = "Motores LLM",
            ["admin.backup"] = "Respaldo",
            ["admin.banUser"] = "Bloquear Usuario",
            ["admin.deleteUser"] = "Eliminar Usuario",
            ["admin.promoteAdmin"] = "Promover a Admin",

            ["hw.cpuUsage"] = "Uso de CPU",
            ["hw.ramUsage"] = "Uso de RAM",
            ["hw.gpuUsage"] = "Uso de GPU",
            ["hw.temperature"] = "Temperatura",
            ["hw.fanSpeed"] = "Velocidad del Ventilador",
            ["hw.diskUsage"] = "Uso del Disco",

            ["llm.slot"] = "Ranura",
            ["llm.mount"] = "Montar",
            ["llm.unmount"] = "Desmontar",
            ["llm.mounted"] = "Montado",
            ["llm.unmounted"] = "Desmontado",
            ["llm.loading"] = "Cargando...",
            ["llm.mainEngine"] = "Motor Principal",
            ["llm.promptCleanup"] = "Limpieza de Prompt",
            ["llm.ttsEngine"] = "Motor TTS",
            ["llm.aiChat"] = "Chat IA",
            ["llm.selectModel"] = "Seleccionar Modelo",
            ["llm.selectRole"] = "Seleccionar Rol",
            ["llm.selectAbility"] = "Seleccionar Habilidad",

            ["ability.agent"] = "Agente",
            ["ability.monitor"] = "Monitor",
            ["ability.dualPower"] = "Potencia Dual Principal",
            ["ability.imageGen"] = "Generación de Imágenes",
            ["ability.tts"] = "Texto a Voz",
            ["ability.promptClean"] = "Limpieza de Prompt",

            ["common.ok"] = "Aceptar",
            ["common.cancel"] = "Cancelar",
            ["common.save"] = "Guardar",
            ["common.delete"] = "Eliminar",
            ["common.refresh"] = "Actualizar",
            ["common.close"] = "Cerrar",
            ["common.yes"] = "Sí",
            ["common.no"] = "No",
            ["common.error"] = "Error",
            ["common.success"] = "Éxito",
            ["common.loading"] = "Cargando...",
            ["common.search"] = "Buscar",
            ["common.status"] = "Estado",
            ["common.name"] = "Nombre",
            ["common.actions"] = "Acciones",

            ["login.title"] = "Iniciar Sesión",
            ["login.username"] = "Usuario",
            ["login.password"] = "Contraseña",
            ["login.signin"] = "Entrar",
            ["login.register"] = "Registrarse",
            ["login.forgotPassword"] = "¿Olvidaste la contraseña?",

            ["msg.allSystemsOk"] = "Todos los sistemas operativos.",
            ["msg.rtpDisabled"] = "La protección en tiempo real está desactivada.",
            ["msg.welcome"] = "Bienvenido, {0}. Todos los sistemas operativos.",
            ["msg.serverDown"] = "El servidor no es accesible.",
            ["msg.exitWarning"] = "Si sales de SGL SyntheticAI, tu sistema quedará vulnerable y desprotegido.",
            ["firewall.quickPresets"] = "Preajustes Rápidos",
            ["firewall.gamingMode"] = "Modo Juego",
            ["firewall.streamingMode"] = "Modo Streaming",
            ["firewall.lockdownMode"] = "Modo Bloqueo",
            ["firewall.activeConnections"] = "Conexiones Activas",
            ["firewall.direction"] = "Dirección",
            ["firewall.protocol"] = "Protocolo",
            ["firewall.localPorts"] = "Puertos Locales",
            ["firewall.remoteAddr"] = "Dir. Remota",
            ["chat.loadModel"] = "Cargar Modelo",
            ["chat.showThinking"] = "Mostrar pensamiento",
            ["chat.imageGen"] = "Generación de Imágenes IA",
            ["chat.prompt"] = "Indicación",
            ["chat.generate"] = "Generar",
            ["quarantine.title"] = "Bóveda de Cuarentena",
            ["quarantine.clearAll"] = "Limpiar Todo",
            ["quarantine.restore"] = "Restaurar",
            ["security.title"] = "Monitor de Seguridad",
            ["security.activeMonitors"] = "Monitores Activos",
            ["security.alerts"] = "Alertas de Seguridad",
            ["security.acknowledge"] = "Confirmar",
            ["security.dismiss"] = "Descartar",
            ["common.download"] = "Descargar",
            ["common.enabled"] = "Habilitado",
            ["common.disabled"] = "Deshabilitado",
        };

        // ── Afrikaans ──
        _translations["af"] = new Dictionary<string, string>
        {
            ["nav.dashboard"] = "Kontroleskerm",
            ["nav.scan"] = "Skandeer",
            ["nav.firewall"] = "Brandmuur",
            ["nav.security"] = "Sekuriteit",
            ["nav.network"] = "Netwerk",
            ["nav.privacy"] = "Privaatheid",
            ["nav.email"] = "E-pos",
            ["nav.quarantine"] = "Kwarantyn",
            ["nav.schedule"] = "Skedule",
            ["nav.fim"] = "FIM",
            ["nav.monitor"] = "Monitor",
            ["nav.threats"] = "Bedreigings",
            ["nav.updates"] = "Opdaterings",
            ["nav.processes"] = "Prosesse",
            ["nav.tools"] = "Gereedskap",
            ["nav.share"] = "Deel",
            ["nav.faq"] = "FAQ",
            ["nav.chat"] = "Gesels",
            ["nav.settings"] = "Instellings",
            ["nav.help"] = "Hulp",
            ["nav.admin"] = "Admin",
            ["nav.uieditor"] = "UI Redigeerder",

            ["dashboard.title"] = "Sekuriteit Kontroleskerm",
            ["dashboard.protection"] = "Beskermingstatus",
            ["dashboard.active"] = "Aktief",
            ["dashboard.inactive"] = "Onaktief",
            ["dashboard.threatlevel"] = "Bedreigingsvlak",
            ["dashboard.lastscan"] = "Laaste Skandering",
            ["dashboard.totalthreats"] = "Totale Bedreigings",
            ["dashboard.quickscan"] = "Vinnige Skandering",
            ["dashboard.fullscan"] = "Volle Skandering",
            ["dashboard.connectedclients"] = "Gekoppelde Kliënte",

            ["scan.title"] = "Antivirus Skandering",
            ["scan.quick"] = "Vinnige Skandering",
            ["scan.extended"] = "Uitgebreide Skandering",
            ["scan.custom"] = "Pasgemaakte Skandering",
            ["scan.start"] = "Begin Skandeer",
            ["scan.stop"] = "Stop Skandeer",
            ["scan.scanning"] = "Skandeer...",
            ["scan.complete"] = "Skandering Voltooi",
            ["scan.threatsFound"] = "Bedreigings Gevind",
            ["scan.filesScanned"] = "Lêers Geskandeer",
            ["scan.noThreats"] = "Geen bedreigings opgespoor",

            ["firewall.title"] = "Brandmuurbestuur",
            ["firewall.rules"] = "Aktiewe Reëls",
            ["firewall.addRule"] = "Voeg Reël By",
            ["firewall.removeRule"] = "Verwyder Reël",
            ["firewall.enabled"] = "Brandmuur Aktief",
            ["firewall.disabled"] = "Brandmuur Onaktief",

            ["chat.title"] = "KI Gesels Assistent",
            ["chat.placeholder"] = "Tik jou boodskap...",
            ["chat.send"] = "Stuur",
            ["chat.clear"] = "Vee Gesels Uit",

            ["settings.title"] = "Instellings",
            ["settings.general"] = "Algemeen",
            ["settings.scanner"] = "Skandeerder",
            ["settings.firewallSettings"] = "Brandmuur",
            ["settings.llm"] = "LLM Konfigurasie",
            ["settings.security"] = "Sekuriteit",
            ["settings.language"] = "Taal",
            ["settings.save"] = "Stoor Instellings",
            ["settings.startWithWindows"] = "Begin met Windows",
            ["settings.minimizeToTray"] = "Minimaliseer na Skinkbord",
            ["settings.realTimeProtection"] = "Intydse Beskerming",

            ["admin.title"] = "Admin Paneel",
            ["admin.users"] = "Gebruikerbestuur",
            ["admin.reports"] = "Verslae",
            ["admin.diagnostics"] = "Diagnostiek",
            ["admin.hardware"] = "Hardeware Monitor",
            ["admin.llmslots"] = "LLM Enjins",
            ["admin.backup"] = "Rugsteun",
            ["admin.banUser"] = "Blokkeer Gebruiker",
            ["admin.deleteUser"] = "Verwyder Gebruiker",
            ["admin.promoteAdmin"] = "Bevorder na Admin",

            ["hw.cpuUsage"] = "SVE Gebruik",
            ["hw.ramUsage"] = "RAM Gebruik",
            ["hw.gpuUsage"] = "GPU Gebruik",
            ["hw.temperature"] = "Temperatuur",
            ["hw.fanSpeed"] = "Waaier Spoed",
            ["hw.diskUsage"] = "Skyf Gebruik",

            ["llm.slot"] = "Gleuf",
            ["llm.mount"] = "Monteer",
            ["llm.unmount"] = "Demonteer",
            ["llm.mounted"] = "Gemonteer",
            ["llm.unmounted"] = "Ongemonteer",
            ["llm.loading"] = "Laai...",
            ["llm.mainEngine"] = "Hoof Enjin",
            ["llm.promptCleanup"] = "Prompt Skoonmaak",
            ["llm.ttsEngine"] = "TTS Enjin",
            ["llm.aiChat"] = "KI Gesels",
            ["llm.selectModel"] = "Kies Model",
            ["llm.selectRole"] = "Kies Rol",
            ["llm.selectAbility"] = "Kies Vermoë",

            ["ability.agent"] = "Agent",
            ["ability.monitor"] = "Monitor",
            ["ability.dualPower"] = "Dubbel Krag Hoof",
            ["ability.imageGen"] = "Beeldgenerering",
            ["ability.tts"] = "Teks na Spraak",
            ["ability.promptClean"] = "Prompt Skoonmaak",

            ["common.ok"] = "Goed",
            ["common.cancel"] = "Kanselleer",
            ["common.save"] = "Stoor",
            ["common.delete"] = "Verwyder",
            ["common.refresh"] = "Herlaai",
            ["common.close"] = "Sluit",
            ["common.yes"] = "Ja",
            ["common.no"] = "Nee",
            ["common.error"] = "Fout",
            ["common.success"] = "Sukses",
            ["common.loading"] = "Laai...",
            ["common.search"] = "Soek",
            ["common.status"] = "Status",
            ["common.name"] = "Naam",
            ["common.actions"] = "Aksies",

            ["login.title"] = "Teken In",
            ["login.username"] = "Gebruikersnaam",
            ["login.password"] = "Wagwoord",
            ["login.signin"] = "Teken In",
            ["login.register"] = "Registreer",
            ["login.forgotPassword"] = "Wagwoord Vergeet?",

            ["msg.allSystemsOk"] = "Alle stelsels werk.",
            ["msg.rtpDisabled"] = "Intydse beskerming is afgeskakel.",
            ["msg.welcome"] = "Welkom, {0}. Alle stelsels werk.",
            ["msg.serverDown"] = "Bediener is onbereikbaar.",
            ["msg.exitWarning"] = "As jy SGL SyntheticAI verlaat, sal jou stelsel kwesbaar en onbeskerm wees.",
            ["firewall.quickPresets"] = "Vinnige Voorafinstellings",
            ["firewall.gamingMode"] = "Spelmodus",
            ["firewall.streamingMode"] = "Stroom Modus",
            ["firewall.lockdownMode"] = "Sluit Modus",
            ["firewall.activeConnections"] = "Aktiewe Verbindings",
            ["firewall.direction"] = "Rigting",
            ["firewall.protocol"] = "Protokol",
            ["firewall.localPorts"] = "Plaaslike Poorte",
            ["firewall.remoteAddr"] = "Afgeleë Adres",
            ["chat.loadModel"] = "Laai Model",
            ["chat.showThinking"] = "Wys denkproses",
            ["chat.imageGen"] = "KI Beeldgenerering",
            ["chat.prompt"] = "Opdrag",
            ["chat.generate"] = "Genereer",
            ["quarantine.title"] = "Kwarantyn Kluis",
            ["quarantine.clearAll"] = "Maak Alles Skoon",
            ["quarantine.restore"] = "Herstel",
            ["security.title"] = "Sekuriteitsmonitor",
            ["security.activeMonitors"] = "Aktiewe Monitors",
            ["security.alerts"] = "Sekuriteitswaarskuwings",
            ["security.acknowledge"] = "Erken",
            ["security.dismiss"] = "Verwerp",
            ["common.download"] = "Aflaai",
            ["common.enabled"] = "Geaktiveer",
            ["common.disabled"] = "Gedeaktiveer",
        };

        // ── Russian ──
        _translations["ru"] = new Dictionary<string, string>
        {
            ["nav.dashboard"] = "Панель",
            ["nav.scan"] = "Сканирование",
            ["nav.firewall"] = "Брандмауэр",
            ["nav.security"] = "Безопасность",
            ["nav.network"] = "Сеть",
            ["nav.privacy"] = "Конфиденциальность",
            ["nav.email"] = "Почта",
            ["nav.quarantine"] = "Карантин",
            ["nav.schedule"] = "Расписание",
            ["nav.fim"] = "FIM",
            ["nav.monitor"] = "Монитор",
            ["nav.threats"] = "Угрозы",
            ["nav.updates"] = "Обновления",
            ["nav.processes"] = "Процессы",
            ["nav.tools"] = "Инструменты",
            ["nav.share"] = "Поделиться",
            ["nav.faq"] = "FAQ",
            ["nav.chat"] = "Чат",
            ["nav.settings"] = "Настройки",
            ["nav.help"] = "Помощь",
            ["nav.admin"] = "Админ",
            ["nav.uieditor"] = "Редактор UI",

            ["dashboard.title"] = "Панель Безопасности",
            ["dashboard.protection"] = "Статус Защиты",
            ["dashboard.active"] = "Активна",
            ["dashboard.inactive"] = "Неактивна",
            ["dashboard.threatlevel"] = "Уровень Угрозы",
            ["dashboard.lastscan"] = "Последнее Сканирование",
            ["dashboard.totalthreats"] = "Всего Угроз",
            ["dashboard.quickscan"] = "Быстрое Сканирование",
            ["dashboard.fullscan"] = "Полное Сканирование",
            ["dashboard.connectedclients"] = "Подключённые Клиенты",

            ["scan.title"] = "Антивирусное Сканирование",
            ["scan.quick"] = "Быстрое Сканирование",
            ["scan.extended"] = "Расширенное Сканирование",
            ["scan.custom"] = "Пользовательское Сканирование",
            ["scan.start"] = "Начать Сканирование",
            ["scan.stop"] = "Остановить",
            ["scan.scanning"] = "Сканирование...",
            ["scan.complete"] = "Сканирование Завершено",
            ["scan.threatsFound"] = "Найдено Угроз",
            ["scan.filesScanned"] = "Файлов Просканировано",
            ["scan.noThreats"] = "Угрозы не обнаружены",

            ["firewall.title"] = "Управление Брандмауэром",
            ["firewall.rules"] = "Активные Правила",
            ["firewall.addRule"] = "Добавить Правило",
            ["firewall.removeRule"] = "Удалить Правило",
            ["firewall.enabled"] = "Брандмауэр Включён",
            ["firewall.disabled"] = "Брандмауэр Выключен",

            ["chat.title"] = "ИИ Чат Ассистент",
            ["chat.placeholder"] = "Введите сообщение...",
            ["chat.send"] = "Отправить",
            ["chat.clear"] = "Очистить",

            ["settings.title"] = "Настройки",
            ["settings.general"] = "Общие",
            ["settings.scanner"] = "Сканер",
            ["settings.firewallSettings"] = "Брандмауэр",
            ["settings.llm"] = "Настройки LLM",
            ["settings.security"] = "Безопасность",
            ["settings.language"] = "Язык",
            ["settings.save"] = "Сохранить",
            ["settings.startWithWindows"] = "Запуск с Windows",
            ["settings.minimizeToTray"] = "Свернуть в Трей",
            ["settings.realTimeProtection"] = "Защита в Реальном Времени",

            ["admin.title"] = "Панель Администратора",
            ["admin.users"] = "Управление Пользователями",
            ["admin.reports"] = "Отчёты",
            ["admin.diagnostics"] = "Диагностика",
            ["admin.hardware"] = "Монитор Оборудования",
            ["admin.llmslots"] = "Модели LLM",
            ["admin.backup"] = "Резервное Копирование",
            ["admin.banUser"] = "Заблокировать",
            ["admin.deleteUser"] = "Удалить Пользователя",
            ["admin.promoteAdmin"] = "Назначить Админом",

            ["hw.cpuUsage"] = "Загрузка ЦП",
            ["hw.ramUsage"] = "Использование ОЗУ",
            ["hw.gpuUsage"] = "Загрузка ГП",
            ["hw.temperature"] = "Температура",
            ["hw.fanSpeed"] = "Скорость Вентилятора",
            ["hw.diskUsage"] = "Использование Диска",

            ["llm.slot"] = "Слот",
            ["llm.mount"] = "Подключить",
            ["llm.unmount"] = "Отключить",
            ["llm.mounted"] = "Подключён",
            ["llm.unmounted"] = "Отключён",
            ["llm.loading"] = "Загрузка...",
            ["llm.mainEngine"] = "Основной Движок",
            ["llm.promptCleanup"] = "Очистка Промпта",
            ["llm.ttsEngine"] = "Движок TTS",
            ["llm.aiChat"] = "ИИ Чат",
            ["llm.selectModel"] = "Выбрать Модель",
            ["llm.selectRole"] = "Выбрать Роль",
            ["llm.selectAbility"] = "Выбрать Способность",

            ["ability.agent"] = "Агент",
            ["ability.monitor"] = "Монитор",
            ["ability.dualPower"] = "Двойная Мощность",
            ["ability.imageGen"] = "Генерация Изображений",
            ["ability.tts"] = "Синтез Речи",
            ["ability.promptClean"] = "Очистка Промпта",

            ["common.ok"] = "ОК",
            ["common.cancel"] = "Отмена",
            ["common.save"] = "Сохранить",
            ["common.delete"] = "Удалить",
            ["common.refresh"] = "Обновить",
            ["common.close"] = "Закрыть",
            ["common.yes"] = "Да",
            ["common.no"] = "Нет",
            ["common.error"] = "Ошибка",
            ["common.success"] = "Успех",
            ["common.loading"] = "Загрузка...",
            ["common.search"] = "Поиск",
            ["common.status"] = "Статус",
            ["common.name"] = "Имя",
            ["common.actions"] = "Действия",

            ["login.title"] = "Вход",
            ["login.username"] = "Логин",
            ["login.password"] = "Пароль",
            ["login.signin"] = "Войти",
            ["login.register"] = "Регистрация",
            ["login.forgotPassword"] = "Забыли пароль?",

            ["msg.allSystemsOk"] = "Все системы работают.",
            ["msg.rtpDisabled"] = "Защита в реальном времени отключена.",
            ["msg.welcome"] = "Добро пожаловать, {0}. Все системы работают.",
            ["msg.serverDown"] = "Сервер недоступен.",
            ["msg.exitWarning"] = "Если вы выйдете из SGL SyntheticAI, ваша система будет уязвима и незащищена.",
            ["firewall.quickPresets"] = "Быстрые Настройки",
            ["firewall.gamingMode"] = "Игровой Режим",
            ["firewall.streamingMode"] = "Режим Стриминга",
            ["firewall.lockdownMode"] = "Режим Блокировки",
            ["firewall.activeConnections"] = "Активные Соединения",
            ["firewall.direction"] = "Направление",
            ["firewall.protocol"] = "Протокол",
            ["firewall.localPorts"] = "Локальные Порты",
            ["firewall.remoteAddr"] = "Удалённый Адрес",
            ["chat.loadModel"] = "Загрузить Модель",
            ["chat.showThinking"] = "Показать размышления",
            ["chat.imageGen"] = "ИИ Генерация Изображений",
            ["chat.prompt"] = "Запрос",
            ["chat.generate"] = "Генерировать",
            ["quarantine.title"] = "Хранилище Карантина",
            ["quarantine.clearAll"] = "Очистить Всё",
            ["quarantine.restore"] = "Восстановить",
            ["security.title"] = "Монитор Безопасности",
            ["security.activeMonitors"] = "Активные Мониторы",
            ["security.alerts"] = "Оповещения Безопасности",
            ["security.acknowledge"] = "Подтвердить",
            ["security.dismiss"] = "Отклонить",
            ["common.download"] = "Скачать",
            ["common.enabled"] = "Включено",
            ["common.disabled"] = "Отключено",
        };

        // ── French ──
        _translations["fr"] = new Dictionary<string, string>
        {
            ["nav.dashboard"] = "Tableau de Bord",
            ["nav.scan"] = "Analyser",
            ["nav.firewall"] = "Pare-feu",
            ["nav.security"] = "Sécurité",
            ["nav.network"] = "Réseau",
            ["nav.privacy"] = "Confidentialité",
            ["nav.email"] = "E-mail",
            ["nav.quarantine"] = "Quarantaine",
            ["nav.schedule"] = "Planifier",
            ["nav.fim"] = "FIM",
            ["nav.monitor"] = "Moniteur",
            ["nav.threats"] = "Menaces",
            ["nav.updates"] = "Mises à Jour",
            ["nav.processes"] = "Processus",
            ["nav.tools"] = "Outils",
            ["nav.share"] = "Partager",
            ["nav.faq"] = "FAQ",
            ["nav.chat"] = "Discussion",
            ["nav.settings"] = "Paramètres",
            ["nav.help"] = "Aide",
            ["nav.admin"] = "Admin",
            ["nav.uieditor"] = "Éditeur UI",

            ["dashboard.title"] = "Tableau de Bord Sécurité",
            ["dashboard.protection"] = "État de la Protection",
            ["dashboard.active"] = "Actif",
            ["dashboard.inactive"] = "Inactif",
            ["dashboard.threatlevel"] = "Niveau de Menace",
            ["dashboard.lastscan"] = "Dernière Analyse",
            ["dashboard.totalthreats"] = "Total des Menaces",
            ["dashboard.quickscan"] = "Analyse Rapide",
            ["dashboard.fullscan"] = "Analyse Complète",
            ["dashboard.connectedclients"] = "Clients Connectés",

            ["scan.title"] = "Analyse Antivirus",
            ["scan.quick"] = "Analyse Rapide",
            ["scan.extended"] = "Analyse Étendue",
            ["scan.custom"] = "Analyse Personnalisée",
            ["scan.start"] = "Démarrer l'Analyse",
            ["scan.stop"] = "Arrêter l'Analyse",
            ["scan.scanning"] = "Analyse en cours...",
            ["scan.complete"] = "Analyse Terminée",
            ["scan.threatsFound"] = "Menaces Détectées",
            ["scan.filesScanned"] = "Fichiers Analysés",
            ["scan.noThreats"] = "Aucune menace détectée",

            ["firewall.title"] = "Gestion du Pare-feu",
            ["firewall.rules"] = "Règles Actives",
            ["firewall.addRule"] = "Ajouter une Règle",
            ["firewall.removeRule"] = "Supprimer la Règle",
            ["firewall.enabled"] = "Pare-feu Activé",
            ["firewall.disabled"] = "Pare-feu Désactivé",

            ["chat.title"] = "Assistant IA",
            ["chat.placeholder"] = "Tapez votre message...",
            ["chat.send"] = "Envoyer",
            ["chat.clear"] = "Effacer",

            ["settings.title"] = "Paramètres",
            ["settings.general"] = "Général",
            ["settings.scanner"] = "Analyseur",
            ["settings.firewallSettings"] = "Pare-feu",
            ["settings.llm"] = "Configuration LLM",
            ["settings.security"] = "Sécurité",
            ["settings.language"] = "Langue",
            ["settings.save"] = "Enregistrer",
            ["settings.startWithWindows"] = "Démarrer avec Windows",
            ["settings.minimizeToTray"] = "Réduire dans la Barre",
            ["settings.realTimeProtection"] = "Protection en Temps Réel",

            ["admin.title"] = "Panneau d'Administration",
            ["admin.users"] = "Gestion des Utilisateurs",
            ["admin.reports"] = "Rapports",
            ["admin.diagnostics"] = "Diagnostics",
            ["admin.hardware"] = "Moniteur Matériel",
            ["admin.llmslots"] = "Moteurs LLM",
            ["admin.backup"] = "Sauvegarde",
            ["admin.banUser"] = "Bannir l'Utilisateur",
            ["admin.deleteUser"] = "Supprimer l'Utilisateur",
            ["admin.promoteAdmin"] = "Promouvoir en Admin",

            ["hw.cpuUsage"] = "Utilisation CPU",
            ["hw.ramUsage"] = "Utilisation RAM",
            ["hw.gpuUsage"] = "Utilisation GPU",
            ["hw.temperature"] = "Température",
            ["hw.fanSpeed"] = "Vitesse du Ventilateur",
            ["hw.diskUsage"] = "Utilisation du Disque",

            ["llm.slot"] = "Emplacement",
            ["llm.mount"] = "Monter",
            ["llm.unmount"] = "Démonter",
            ["llm.mounted"] = "Monté",
            ["llm.unmounted"] = "Non Monté",
            ["llm.loading"] = "Chargement...",
            ["llm.mainEngine"] = "Moteur Principal",
            ["llm.promptCleanup"] = "Nettoyage du Prompt",
            ["llm.ttsEngine"] = "Moteur TTS",
            ["llm.aiChat"] = "Chat IA",
            ["llm.selectModel"] = "Choisir le Modèle",
            ["llm.selectRole"] = "Choisir le Rôle",
            ["llm.selectAbility"] = "Choisir la Capacité",

            ["ability.agent"] = "Agent",
            ["ability.monitor"] = "Moniteur",
            ["ability.dualPower"] = "Double Puissance",
            ["ability.imageGen"] = "Génération d'Images",
            ["ability.tts"] = "Synthèse Vocale",
            ["ability.promptClean"] = "Nettoyage du Prompt",

            ["common.ok"] = "OK",
            ["common.cancel"] = "Annuler",
            ["common.save"] = "Enregistrer",
            ["common.delete"] = "Supprimer",
            ["common.refresh"] = "Rafraîchir",
            ["common.close"] = "Fermer",
            ["common.yes"] = "Oui",
            ["common.no"] = "Non",
            ["common.error"] = "Erreur",
            ["common.success"] = "Succès",
            ["common.loading"] = "Chargement...",
            ["common.search"] = "Rechercher",
            ["common.status"] = "Statut",
            ["common.name"] = "Nom",
            ["common.actions"] = "Actions",

            ["login.title"] = "Connexion",
            ["login.username"] = "Nom d'utilisateur",
            ["login.password"] = "Mot de passe",
            ["login.signin"] = "Se Connecter",
            ["login.register"] = "S'inscrire",
            ["login.forgotPassword"] = "Mot de passe oublié?",

            ["msg.allSystemsOk"] = "Tous les systèmes sont opérationnels.",
            ["msg.rtpDisabled"] = "La protection en temps réel est désactivée.",
            ["msg.welcome"] = "Bienvenue, {0}. Tous les systèmes sont opérationnels.",
            ["msg.serverDown"] = "Le serveur est inaccessible.",
            ["msg.exitWarning"] = "Si vous quittez SGL SyntheticAI, votre système sera vulnérable et non protégé.",
            ["firewall.quickPresets"] = "Préréglages Rapides",
            ["firewall.gamingMode"] = "Mode Jeu",
            ["firewall.streamingMode"] = "Mode Streaming",
            ["firewall.lockdownMode"] = "Mode Verrouillage",
            ["firewall.activeConnections"] = "Connexions Actives",
            ["firewall.direction"] = "Direction",
            ["firewall.protocol"] = "Protocole",
            ["firewall.localPorts"] = "Ports Locaux",
            ["firewall.remoteAddr"] = "Adresse Distante",
            ["chat.loadModel"] = "Charger Modèle",
            ["chat.showThinking"] = "Afficher la réflexion",
            ["chat.imageGen"] = "Génération d'Images IA",
            ["chat.prompt"] = "Invite",
            ["chat.generate"] = "Générer",
            ["quarantine.title"] = "Coffre de Quarantaine",
            ["quarantine.clearAll"] = "Tout Effacer",
            ["quarantine.restore"] = "Restaurer",
            ["security.title"] = "Moniteur de Sécurité",
            ["security.activeMonitors"] = "Moniteurs Actifs",
            ["security.alerts"] = "Alertes de Sécurité",
            ["security.acknowledge"] = "Confirmer",
            ["security.dismiss"] = "Ignorer",
            ["common.download"] = "Télécharger",
            ["common.enabled"] = "Activé",
            ["common.disabled"] = "Désactivé",
        };

        // ── Chinese Mandarin ──
        _translations["zh"] = new Dictionary<string, string>
        {
            ["nav.dashboard"] = "仪表盘",
            ["nav.scan"] = "扫描",
            ["nav.firewall"] = "防火墙",
            ["nav.security"] = "安全",
            ["nav.network"] = "网络",
            ["nav.privacy"] = "隐私",
            ["nav.email"] = "邮件扫描",
            ["nav.quarantine"] = "隔离区",
            ["nav.schedule"] = "计划任务",
            ["nav.fim"] = "文件监控",
            ["nav.monitor"] = "监控",
            ["nav.threats"] = "威胁",
            ["nav.updates"] = "更新",
            ["nav.processes"] = "进程",
            ["nav.tools"] = "工具",
            ["nav.share"] = "分享",
            ["nav.faq"] = "常见问题",
            ["nav.chat"] = "聊天",
            ["nav.settings"] = "设置",
            ["nav.help"] = "帮助",
            ["nav.admin"] = "管理",
            ["nav.uieditor"] = "界面编辑",

            ["dashboard.title"] = "安全仪表盘",
            ["dashboard.protection"] = "保护状态",
            ["dashboard.active"] = "活跃",
            ["dashboard.inactive"] = "未激活",
            ["dashboard.threatlevel"] = "威胁等级",
            ["dashboard.lastscan"] = "上次扫描",
            ["dashboard.totalthreats"] = "威胁总数",
            ["dashboard.quickscan"] = "快速扫描",
            ["dashboard.fullscan"] = "全面扫描",
            ["dashboard.connectedclients"] = "已连接客户端",

            ["scan.title"] = "杀毒扫描",
            ["scan.quick"] = "快速扫描",
            ["scan.extended"] = "深度扫描",
            ["scan.custom"] = "自定义扫描",
            ["scan.start"] = "开始扫描",
            ["scan.stop"] = "停止扫描",
            ["scan.scanning"] = "扫描中...",
            ["scan.complete"] = "扫描完成",
            ["scan.threatsFound"] = "发现威胁",
            ["scan.filesScanned"] = "已扫描文件",
            ["scan.noThreats"] = "未发现威胁",

            ["firewall.title"] = "防火墙管理",
            ["firewall.rules"] = "活跃规则",
            ["firewall.addRule"] = "添加规则",
            ["firewall.removeRule"] = "删除规则",
            ["firewall.enabled"] = "防火墙已启用",
            ["firewall.disabled"] = "防火墙已禁用",

            ["chat.title"] = "AI 聊天助手",
            ["chat.placeholder"] = "输入消息...",
            ["chat.send"] = "发送",
            ["chat.clear"] = "清除",

            ["settings.title"] = "设置",
            ["settings.general"] = "常规",
            ["settings.scanner"] = "扫描器",
            ["settings.firewallSettings"] = "防火墙",
            ["settings.llm"] = "LLM 配置",
            ["settings.security"] = "安全",
            ["settings.language"] = "语言",
            ["settings.save"] = "保存设置",
            ["settings.startWithWindows"] = "随Windows启动",
            ["settings.minimizeToTray"] = "最小化到托盘",
            ["settings.realTimeProtection"] = "实时保护",

            ["admin.title"] = "管理面板",
            ["admin.users"] = "用户管理",
            ["admin.reports"] = "报告",
            ["admin.diagnostics"] = "诊断",
            ["admin.hardware"] = "硬件监控",
            ["admin.llmslots"] = "LLM 引擎",
            ["admin.backup"] = "备份",
            ["admin.banUser"] = "封禁用户",
            ["admin.deleteUser"] = "删除用户",
            ["admin.promoteAdmin"] = "提升为管理员",

            ["hw.cpuUsage"] = "CPU 使用率",
            ["hw.ramUsage"] = "内存使用率",
            ["hw.gpuUsage"] = "GPU 使用率",
            ["hw.temperature"] = "温度",
            ["hw.fanSpeed"] = "风扇转速",
            ["hw.diskUsage"] = "磁盘使用率",

            ["llm.slot"] = "插槽",
            ["llm.mount"] = "挂载",
            ["llm.unmount"] = "卸载",
            ["llm.mounted"] = "已挂载",
            ["llm.unmounted"] = "未挂载",
            ["llm.loading"] = "加载中...",
            ["llm.mainEngine"] = "主引擎",
            ["llm.promptCleanup"] = "提示词清理",
            ["llm.ttsEngine"] = "TTS 引擎",
            ["llm.aiChat"] = "AI 聊天",
            ["llm.selectModel"] = "选择模型",
            ["llm.selectRole"] = "选择角色",
            ["llm.selectAbility"] = "选择能力",

            ["ability.agent"] = "代理",
            ["ability.monitor"] = "监控",
            ["ability.dualPower"] = "双核主力",
            ["ability.imageGen"] = "图像生成",
            ["ability.tts"] = "文字转语音",
            ["ability.promptClean"] = "提示词清理",

            ["common.ok"] = "确定",
            ["common.cancel"] = "取消",
            ["common.save"] = "保存",
            ["common.delete"] = "删除",
            ["common.refresh"] = "刷新",
            ["common.close"] = "关闭",
            ["common.yes"] = "是",
            ["common.no"] = "否",
            ["common.error"] = "错误",
            ["common.success"] = "成功",
            ["common.loading"] = "加载中...",
            ["common.search"] = "搜索",
            ["common.status"] = "状态",
            ["common.name"] = "名称",
            ["common.actions"] = "操作",

            ["login.title"] = "登录",
            ["login.username"] = "用户名",
            ["login.password"] = "密码",
            ["login.signin"] = "登录",
            ["login.register"] = "注册",
            ["login.forgotPassword"] = "忘记密码?",

            ["msg.allSystemsOk"] = "所有系统运行正常。",
            ["msg.rtpDisabled"] = "实时保护已关闭。",
            ["msg.welcome"] = "欢迎，{0}。所有系统运行正常。",
            ["msg.serverDown"] = "服务器不可达。",
            ["msg.exitWarning"] = "如果退出 SGL SyntheticAI，您的系统将处于无保护状态。",
            ["firewall.quickPresets"] = "快速预设",
            ["firewall.gamingMode"] = "游戏模式",
            ["firewall.streamingMode"] = "流媒体模式",
            ["firewall.lockdownMode"] = "锁定模式",
            ["firewall.activeConnections"] = "活动连接",
            ["firewall.direction"] = "方向",
            ["firewall.protocol"] = "协议",
            ["firewall.localPorts"] = "本地端口",
            ["firewall.remoteAddr"] = "远程地址",
            ["chat.loadModel"] = "加载模型",
            ["chat.showThinking"] = "显示思考过程",
            ["chat.imageGen"] = "AI 图像生成",
            ["chat.prompt"] = "提示词",
            ["chat.generate"] = "生成",
            ["quarantine.title"] = "隔离保险库",
            ["quarantine.clearAll"] = "全部清除",
            ["quarantine.restore"] = "恢复",
            ["security.title"] = "安全监控",
            ["security.activeMonitors"] = "活动监控",
            ["security.alerts"] = "安全警报",
            ["security.acknowledge"] = "确认",
            ["security.dismiss"] = "忽略",
            ["common.download"] = "下载",
            ["common.enabled"] = "已启用",
            ["common.disabled"] = "已禁用",
        };
    }
}

/// <summary>
/// Info about a supported language.
/// </summary>
public class LanguageInfo
{
    public string Code { get; }
    public string DisplayName { get; }
    public string FlagCode { get; }

    /// <summary>
    /// Unicode flag emoji derived from the country flag code.
    /// </summary>
    public string FlagEmoji { get; }

    public LanguageInfo(string code, string displayName, string flagCode)
    {
        Code = code;
        DisplayName = displayName;
        FlagCode = flagCode;
        FlagEmoji = FlagCodeToEmoji(flagCode);
    }

    private static string FlagCodeToEmoji(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode) || countryCode.Length != 2)
            return "\U0001F310"; // globe emoji fallback

        var upper = countryCode.ToUpperInvariant();
        return string.Concat(
            char.ConvertFromUtf32(0x1F1E6 + (upper[0] - 'A')),
            char.ConvertFromUtf32(0x1F1E6 + (upper[1] - 'A'))
        );
    }

    public override string ToString() => $"{FlagEmoji} {DisplayName}";
}
