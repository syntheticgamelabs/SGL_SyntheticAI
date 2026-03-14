namespace SGL.JudgeDredd.LinuxClient;

public class CommandHandler
{
    private readonly ConfigStore _config;
    private readonly LinuxApiClient _api;

    public CommandHandler(ConfigStore config, LinuxApiClient api)
    {
        _config = config;
        _api = api;
    }

    public async Task HandleAsync(string input)
    {
        var parts = input.Trim().Split(' ', 2);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1] : "";

        switch (command)
        {
            case "register": await HandleRegister(); break;
            case "login": await HandleLogin(); break;
            case "logout":
                _config.Clear();
                Console.WriteLine("Logged out.");
                break;
            case "status":
                if (!EnsureAuth()) break;
                Console.WriteLine(await _api.GetServerStatusAsync());
                break;
            case "chat":
                if (!EnsureAuth()) break;
                await HandleChat(args);
                break;
            case "faq":
                if (!EnsureAuth()) break;
                await HandleFaq(args);
                break;
            case "server":
                Console.WriteLine($"Server: {_config.ServerUrl}");
                break;
            case "set-server":
                if (string.IsNullOrWhiteSpace(args))
                { Console.WriteLine("Usage: set-server <url>"); break; }
                _config.ServerUrl = args.Trim();
                _config.Save();
                Console.WriteLine($"Server URL set to: {_config.ServerUrl}");
                break;
            case "help": PrintHelp(); break;
            case "exit": case "quit": Environment.Exit(0); break;
            default:
                Console.WriteLine($"Unknown command: '{command}'. Type 'help' for available commands.");
                break;
        }
    }

    private async Task HandleRegister()
    {
        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Password: ");
        var password = ReadPassword();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        { Console.WriteLine("Username and password are required."); return; }
        var (_, message) = await _api.RegisterAsync(username, password);
        Console.WriteLine(message);
    }

    private async Task HandleLogin()
    {
        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? "";
        Console.Write("Password: ");
        var password = ReadPassword();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        { Console.WriteLine("Username and password are required."); return; }
        var (_, message) = await _api.LoginAsync(username, password);
        Console.WriteLine(message);
    }

    private async Task HandleChat(string args)
    {
        if (!string.IsNullOrWhiteSpace(args))
        {
            Console.WriteLine($"\n{await _api.ChatAsync(args)}\n");
            return;
        }
        Console.WriteLine("Entering AI chat mode. Type 'exit' to return.\n");
        while (true)
        {
            Console.Write("You: ");
            var input = Console.ReadLine()?.Trim() ?? "";
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
            if (string.IsNullOrEmpty(input)) continue;
            Console.Write("AI: ");
            Console.WriteLine(await _api.ChatAsync(input));
            Console.WriteLine();
        }
    }

    private async Task HandleFaq(string args)
    {
        if (args.StartsWith("ask ", StringComparison.OrdinalIgnoreCase))
        {
            var question = args[4..].Trim();
            if (string.IsNullOrEmpty(question))
            { Console.WriteLine("Usage: faq ask <question>"); return; }
            Console.WriteLine(await _api.AskFaqAsync(question));
            return;
        }
        var items = await _api.GetFaqAsync();
        if (items.Count == 0)
        { Console.WriteLine("No FAQ entries. Use 'faq ask <question>' to ask one."); return; }
        foreach (var item in items)
        {
            Console.WriteLine($"Q: {item.Question}");
            Console.WriteLine(item.IsAnswered ? $"A: {item.Answer}" : "A: (awaiting answer)");
            Console.WriteLine($"   - {item.AskedBy}, {item.AskedAt:MMM dd yyyy}\n");
        }
    }

    private bool EnsureAuth()
    {
        if (_config.IsLoggedIn) return true;
        Console.WriteLine("Not logged in. Use 'register' or 'login' first.");
        return false;
    }

    private void PrintHelp()
    {
        Console.WriteLine(@"
SGL SyntheticAI Linux CLI - Commands:
  register     - Register a new account
  login        - Login to existing account
  logout       - Clear saved credentials
  status       - Show server status
  chat [msg]   - AI security chat (or enter interactive mode)
  faq          - View FAQ entries
  faq ask <q>  - Submit a FAQ question
  server       - Show current server URL
  set-server   - Set server URL
  help         - Show this help
  exit         - Quit
");
    }

    private static string ReadPassword()
    {
        var password = "";
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            { password = password[..^1]; Console.Write("\b \b"); }
            else if (!char.IsControl(key.KeyChar))
            { password += key.KeyChar; Console.Write("*"); }
        }
        return password;
    }
}
