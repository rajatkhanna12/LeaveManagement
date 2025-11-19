using System.Net.Mail;
using System.Net;

namespace LeaveManagement
{
    public class EmailService 
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string ccEmail = null)
        {
            var smtpHost = _config["EmailSettings:SmtpHost"];
            var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"]);
            var smtpUser = _config["EmailSettings:SmtpUser"];
            var smtpPass = _config["EmailSettings:SmtpPass"];
            var enableSsl = bool.Parse(_config["EmailSettings:EnableSsl"]);

            using (var smtp = new SmtpClient(smtpHost, smtpPort))
            {
                smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
                smtp.EnableSsl = enableSsl;

                var mail = new MailMessage
                {
                    From = new MailAddress(smtpUser, "Business Box Team"),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mail.To.Add(toEmail);
                // ⭐ CC Added (Manager Email)
                if (!string.IsNullOrWhiteSpace(ccEmail))
                {
                    mail.CC.Add(ccEmail);
                }

                try
                {
                    await smtp.SendMailAsync(mail);
                    Console.WriteLine($"Email sent to {toEmail}, CC: {ccEmail}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");
                }
            }
        }
    }
}
