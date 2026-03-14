namespace SGL.JudgeDredd.LinuxClient;

class Program
{
    static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  ======================================
   SGL SyntheticAI - AI Security Suite
   Linux CLI Client v1.0.0
  ======================================
");
        Console.ResetColor();

        var config = new ConfigStore();
        var api = new LinuxApiClient(config);
        var handler = new CommandHandler(config, api);

        if (config.IsLoggedIn)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Logged in as: {config.Username}");
            Console.WriteLine($"  Server: {config.ServerUrl}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Not logged in. Use 'register' or 'login' to get started.");
            Console.ResetColor();
        }

        Console.WriteLine("  Type 'help' for available commands.\n");

        // If args passed, execute single command and exit
        if (args.Length > 0)
        {
            await handler.HandleAsync(string.Join(' ', args));
            return;
        }

        // Interactive mode
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("SyntheticAI> ");
            Console.ResetColor();

            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;

            await handler.HandleAsync(input);
        }
    }
}
