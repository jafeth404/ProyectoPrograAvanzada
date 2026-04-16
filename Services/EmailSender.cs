using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net;
using System.Net.Mail;

public class EmailSender : IEmailSender
{
    private readonly string _smtpServer;
    private readonly int    _port;
    private readonly string _senderEmail;
    private readonly string _senderPassword;

    public EmailSender(IConfiguration configuration)
    {
        var s        = configuration.GetSection("EmailSettings");
        _smtpServer  = s["SmtpServer"]      ?? throw new InvalidOperationException("EmailSettings:SmtpServer is missing.");
        _port        = s.GetValue<int>("Port");
        _senderEmail = s["SenderEmail"]     ?? throw new InvalidOperationException("EmailSettings:SenderEmail is missing.");
        _senderPassword = s["SenderPassword"] ?? throw new InvalidOperationException("EmailSettings:SenderPassword is missing.");
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var smtp = new SmtpClient(_smtpServer, _port)
        {
            Credentials = new NetworkCredential(_senderEmail, _senderPassword),
            EnableSsl   = true
        };

        var message = new MailMessage
        {
            From       = new MailAddress(_senderEmail),
            Subject    = subject,
            Body       = htmlMessage,
            IsBodyHtml = true
        };

        message.To.Add(email);

        await smtp.SendMailAsync(message);
    }
}