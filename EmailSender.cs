using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using System;

namespace JCOP.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly SmtpClient _smtpClient;

        public EmailSender(IConfiguration configuration)
        {
            var smtpConfig = configuration.GetSection("SmtpConfig");
            string host = smtpConfig["Host"];
            int port = int.TryParse(smtpConfig["Port"], out int parsedPort) ? parsedPort : 0;
            string username = smtpConfig["Username"];
            string password = smtpConfig["Password"];

            if (string.IsNullOrEmpty(host) || port == 0 || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                throw new ApplicationException("SmtpConfig is missing or incomplete in appsettings.json.");
            }

            _smtpClient = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
            };
        }

        public async Task SendEmailAsync(string email, string subject, string body)
        {
            var message = new MailMessage();
            message.From = new MailAddress("customer@joonbeauty.com");
            message.To.Add(email);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            // CC recipients
            message.CC.Add(new MailAddress("customer@joonbeauty.com"));
            message.CC.Add(new MailAddress("vuks@cvatechventures.com"));

            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            try
            {
                await _smtpClient.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                throw;
            }
            finally
            {
                message.Dispose();
            }
        }
    }
}
