using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;

public class EmailSender : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new NetworkCredential("jdavidld0506@gmail.com", "yclf taln ojgd euzq"),
            EnableSsl = true
        };

        var message = new MailMessage
        {
            From = new MailAddress("jdavidld0506@gmail.com"),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };

        message.To.Add(email);

        await smtp.SendMailAsync(message);
    }
}