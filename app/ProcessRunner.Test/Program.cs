using ReviewApp.Infrastructure;

namespace ProcessRunnerTest;

class Program
{
    static async Task Main(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var processRunner = new ProcessRunner(currentDirectory);
        await processRunner.RunAsync("pwsh.exe", "-c \"Get-ChildItem -File\"");
        Console.WriteLine("Hello, World!");
    }
}
