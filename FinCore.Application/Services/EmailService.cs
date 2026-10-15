using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;

namespace FinCore.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration configuration;
        private readonly ISmtpClientWrapper smtpClient;
        ILogger<EmailService> logger;

        public EmailService(IConfiguration configuration, ISmtpClientWrapper smtpClient, ILogger<EmailService> logger)
        {
            this.configuration = configuration;
            this.smtpClient = smtpClient;
            this.logger = logger;
        }

        public async Task<SendEmailResponse> Send(string to, string subject, string body)
        {
            try
            {
                logger.LogInformation("Starting to send email to {Recipient}", to);

                ValidateAddressWhereEmailSent(to);
                logger.LogInformation("Validated recipient email: {Recipient}", to);

                var normalizeSubject = NormalizeSubject(subject);
                logger.LogInformation("Normalized subject: {Subject}", normalizeSubject);

                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(configuration["EmailSettings:Sender"]));
                email.To.Add(MailboxAddress.Parse(to.Trim()));
                email.Subject = normalizeSubject;
                email.Body = new TextPart(TextFormat.Html) { Text = body };

                logger.LogInformation("Connection to SMTP server: {Server}:{Port}", configuration["EmailSettings:SmtpServer"], configuration["EmailSettings:Port"]);

                await smtpClient.ConnectAsync(
                    configuration["EmailSettings:SmtpServer"],
                    int.Parse(configuration["EmailSettings:Port"]),
                    bool.Parse(configuration["EmailSettings:UseSsl"])
                );

                logger.LogInformation("Authenticating with SMTP server...");

                await smtpClient.AuthenticateAsync(
                    configuration["EmailSettings:Username"],
                    configuration["EmailSettings:Password"]
                );

                logger.LogInformation("Sending email to {Recipient}", to);
                await smtpClient.SendAsync(email);

                logger.LogInformation("Disconnecting from SMTP server...");
                await smtpClient.DisconnectAsync(true);

                logger.LogInformation("Email sent successfully to {Recipient}", to);

                return new SendEmailResponse(true, $"Message sent successfully to: {to}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send email to {Recipient}: {Message}", to, ex.Message);
                return new SendEmailResponse(success: false, message: ex.Message, error: ex);
            }
        }

        private void ValidateAddressWhereEmailSent(string to)
        {

            if (string.IsNullOrWhiteSpace(to)) throw new BadRequestException("Email address cannot be empty");

            try
            {
                var mailAddress = new MailAddress(to);
                if (!mailAddress.Address.Contains("@") || !mailAddress.Address.Contains("."))
                {
                    throw new BadRequestException($"Invalid email address format {to}.");
                }

            }
            catch
            {
                throw new BadRequestException($"Sending {to} Email Must Be a Valid Email Address!");
            }
        }


        private string NormalizeSubject(string subject)
        {
            return string.IsNullOrWhiteSpace(subject) ? string.Empty : subject.Trim();
        }
    }
}
