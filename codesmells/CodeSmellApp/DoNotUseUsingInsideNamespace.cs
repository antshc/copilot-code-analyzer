
namespace CodeSmellApp
{
    using System.Net.Mail;

    public class DoNotUseUsingInsideNamespace
    {
        private readonly List<string> m_customers = new();

        public void AddCustomer(string name)
        {
            m_customers.Add(name);
            SaveToDisk();
            SendWelcomeEmail(name);
            LogAction($"Customer {name} added at {DateTime.UtcNow:o}");
        }

        public string GenerateCsvReport1()
        {
            return string.Join(',', m_customers);
        }

        private void SaveToDisk()
        {
            File.WriteAllLines("customers.txt", m_customers);
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
}
