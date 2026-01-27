using System.IO;
using System.IO;
using System.Net.Mail;

namespace CodeSmellApp;

public class AllInOneCustomerManager
{
    private readonly List<string> _customers = new();

    public void AddCustomer(string name)
    {
        _customers.Add(name);
        SaveToDisk();
        SendWelcomeEmail(name);
        LogAction($"Customer {name} added at {DateTime.UtcNow:o}");
    }

    public string GenerateCsvReport()
    {
        return string.Join(',', _customers);
    }

    private void SaveToDisk()
    {
        File.WriteAllLines("customers.txt", _customers);
    }

    private void SendWelcomeEmail(string name)
    {
        using var client = new SmtpClient("smtp.example.com");
        using var message = new MailMessage("noreply@example.com", $"{name}@example.com")
        {
            Subject = "Welcome!",
            Body = "Thanks for joining our fictional customer portal."
        };

        client.Send(message);
    }

    private void LogAction(string message)
    {
        Console.WriteLine($"[AUDIT] {message}");
    }
}
