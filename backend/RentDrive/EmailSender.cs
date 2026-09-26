using Microsoft.AspNetCore.Identity;
using RentDrive.db.models;
using System.Net;
using System.Net.Mail;

namespace RentDrive
{
    public class EmailSender : IEmailSender<User>
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
        {
            var subject = "Подтверждение регистрации на RentDrive";
            var body = $"<p>Для подтверждения вашей почты пройдите по <a href='{confirmationLink}'>ссылке</a>.</p>";
            await SendEmailAsync(email, subject, body);
        }

        public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            var subject = "Код сброса пароля на RentDrive";
            var body = $"<p>Ваш код для сброса пароля: <b>{resetCode}</b></p>";
            await SendEmailAsync(email, subject, body);
        }

        public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            var subject = "Сброс пароля на RentDrive";
            var body = $"<p>Для сброса пароля пройдите по <a href='{resetLink}'>ссылке</a>.</p>";
            await SendEmailAsync(email, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var password = _configuration["EmailSettings:Password"];

            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail!, "RentDriveSupport"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mailMessage);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
