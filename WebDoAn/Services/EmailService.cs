using System.Net;
using System.Net.Mail;
namespace WebDoAn.Services;
public class EmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config) { _config = config; }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
    {
        var settings = _config.GetSection("EmailSettings");
        using var client = new SmtpClient(settings["MailServer"], int.Parse(settings["MailPort"]))
        {
            Credentials = new NetworkCredential(settings["SenderEmail"], settings["Password"]),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(settings["SenderEmail"], settings["SenderName"]),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);
        await client.SendMailAsync(mailMessage);
    }
}