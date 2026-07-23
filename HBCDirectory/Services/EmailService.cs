using brevo_csharp.Api;
using brevo_csharp.Client;
using brevo_csharp.Model;
using HBCDirectory.Services.EmailTemplates;
using Task = System.Threading.Tasks.Task;
using Configuration = brevo_csharp.Client.Configuration;

namespace HBCDirectory.Services
{
    public class EmailService
    {
        private readonly string _apiKey;
        private readonly string _fromEmail;
        private readonly string _fromName = "Heritage Baptist Church";

        public EmailService(IConfiguration config)
        {
            _apiKey    = config["BrevoApiKey"]  ?? throw new InvalidOperationException("BrevoApiKey not configured.");
            _fromEmail = config["FromEmail"]     ?? throw new InvalidOperationException("FromEmail not configured.");
        }

        //  Generic send 

        public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            Configuration.Default.ApiKey["api-key"] = _apiKey;
            var api = new TransactionalEmailsApi();

            var email = new SendSmtpEmail(
                sender: new SendSmtpEmailSender(_fromName, _fromEmail),
                to: new List<SendSmtpEmailTo> { new SendSmtpEmailTo(toEmail, toName) },
                subject: subject,
                htmlContent: htmlBody
            );

            await api.SendTransacEmailAsync(email);
        }

        //  Welcome email — sent when admin adds a new member 

        public async Task SendWelcomeEmailAsync(
            string toEmail,
            string memberName,
            string temporaryPassword,
            string resetPasswordLink)
        {
            var html = WelcomeEmailTemplate.Html
                .Replace("{memberName}",       memberName)
                .Replace("{username}",         toEmail)
                .Replace("{temporaryPassword}", temporaryPassword)
                .Replace("{resetPasswordLink}", resetPasswordLink);

            await SendAsync(toEmail, memberName,
                "Welcome to the HBC Member Directory", html);
        }

        //  Password reset email — sent when member clicks Forgot Password 

        public async Task SendPasswordResetEmailAsync(
            string toEmail,
            string memberName,
            string resetPasswordLink)
        {
            var html = PasswordResetEmailTemplate.Html
                .Replace("{memberName}",        memberName)
                .Replace("{resetPasswordLink}", resetPasswordLink);

            await SendAsync(toEmail, memberName,
                "Reset Your HBC Directory Password", html);
        }
    }
}
