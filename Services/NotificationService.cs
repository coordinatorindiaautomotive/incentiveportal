using System.Net;
using System.Net.Mail;
using System.Text.Json;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Services;

public interface INotificationService
{
    Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
    Task<bool> SendSmsAsync(string mobileNo, string message, CancellationToken cancellationToken = default);
    Task<bool> SendPasswordResetNotificationAsync(string usernameOrEmail, string tempPassword, CancellationToken cancellationToken = default);
    Task<bool> TestSmtpSettingsAsync(NotificationSetting setting, string decryptedPassword, string testRecipient, CancellationToken cancellationToken = default);
    Task<bool> TestSmsSettingsAsync(NotificationSetting setting, string testRecipient, CancellationToken cancellationToken = default);
}

public sealed class NotificationService : INotificationService
{
    private readonly IncentiveDbContext _db;
    private readonly IDataProtector _protector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IncentiveDbContext db,
        IDataProtectionProvider protectionProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _protector = protectionProvider.CreateProtector("ControlTowerSettings");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var setting = await _db.NotificationSettings
            .Where(x => !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting == null || !setting.EmailEnabled)
        {
            _logger.LogWarning("Email delivery is disabled or setting is missing.");
            return false;
        }

        string password = "";
        if (!string.IsNullOrEmpty(setting.SmtpPassEncrypted))
        {
            try
            {
                password = _protector.Unprotect(setting.SmtpPassEncrypted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt SMTP password.");
            }
        }

        return await SendSmtpCoreAsync(setting, password, to, subject, body, cancellationToken);
    }

    public async Task<bool> SendSmsAsync(string mobileNo, string message, CancellationToken cancellationToken = default)
    {
        var setting = await _db.NotificationSettings
            .Where(x => !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting == null || !setting.SmsEnabled)
        {
            _logger.LogWarning("SMS dispatch is disabled or setting is missing.");
            return false;
        }

        return await SendSmsCoreAsync(setting, mobileNo, message, cancellationToken);
    }

    public async Task<bool> SendPasswordResetNotificationAsync(string usernameOrEmail, string tempPassword, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .Where(u => (u.UserName == usernameOrEmail || u.Email == usernameOrEmail) && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Password reset requested for non-existent user: {User}", usernameOrEmail);
            return false;
        }

        var setting = await _db.NotificationSettings
            .Where(x => !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (setting == null)
        {
            _logger.LogWarning("Notification settings missing. Cannot send password reset.");
            return false;
        }

        bool emailSent = false;
        bool smsSent = false;

        string subject = "Incentive Portal - Password Reset";
        string body = $@"Hello {user.UserName},

Your password has been reset. Please find your temporary password below:

Temporary Password: {tempPassword}

Please log in and change your password immediately.

Regards,
Incentive Portal Admin";

        if (setting.EmailEnabled && !string.IsNullOrEmpty(user.Email))
        {
            emailSent = await SendEmailAsync(user.Email, subject, body, cancellationToken);
        }

        if (setting.SmsEnabled)
        {
            // Try to find mobile number from branch or user profile if it existed.
            // Since User model doesn't have a direct MobileNo, let's see if we can look up Executive or Branch.
            // Or if user email can be used as mobile fallback or if there's any mobile associated.
            // Let's check if the user is a branch manager or executive with a mobile number.
            var branch = user.BranchId.HasValue 
                ? await _db.Branches.FindAsync(new object[] { user.BranchId.Value }, cancellationToken) 
                : null;
            string mobile = branch?.MobileNo ?? "";

            if (!string.IsNullOrEmpty(mobile))
            {
                string smsMessage = $"Hello {user.UserName}, your temporary password for Incentive Portal is: {tempPassword}. Please change it after login.";
                smsSent = await SendSmsAsync(mobile, smsMessage, cancellationToken);
            }
        }

        return emailSent || smsSent;
    }

    public async Task<bool> TestSmtpSettingsAsync(NotificationSetting setting, string decryptedPassword, string testRecipient, CancellationToken cancellationToken = default)
    {
        string subject = "Incentive Portal - SMTP Test Mail";
        string body = "This is a test email sent from the Incentive Portal Control Tower to verify SMTP settings. If you received this, the SMTP server configuration is correct!";
        return await SendSmtpCoreAsync(setting, decryptedPassword, testRecipient, subject, body, cancellationToken);
    }

    public async Task<bool> TestSmsSettingsAsync(NotificationSetting setting, string testRecipient, CancellationToken cancellationToken = default)
    {
        string message = "Incentive Portal - MSG91 Test SMS. If you received this, your SMS gateway configuration is correct!";
        return await SendSmsCoreAsync(setting, testRecipient, message, cancellationToken);
    }

    private async Task<bool> SendSmtpCoreAsync(NotificationSetting setting, string password, string to, string subject, string body, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(setting.SmtpHost, setting.SmtpPort)
            {
                EnableSsl = setting.SmtpUseSsl,
                Credentials = new NetworkCredential(setting.SmtpUser, password),
                Timeout = 10000
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(setting.FromEmail, setting.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = body.Contains("<html>") || body.Contains("<p>") || body.Contains("<div")
            };

            mailMessage.To.Add(to);
            await client.SendMailAsync(mailMessage, cancellationToken);
            _logger.LogInformation("Email successfully sent to {To}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending SMTP email to {To}", to);
            return false;
        }
    }

    private async Task<bool> SendSmsCoreAsync(NotificationSetting setting, string mobileNo, string message, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            
            // Standard MSG91 flow API call structure
            var payload = new
            {
                template_id = "default_template", // Can be customized or parameterized
                sender = setting.SmsSenderId,
                short_url = "0",
                mobiles = mobileNo,
                message = message
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.msg91.com/api/v5/flow/")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("authkey", setting.SmsApiKey);

            var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS successfully sent via MSG91 to {Mobile}", mobileNo);
                return true;
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("MSG91 API returned failure: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while sending MSG91 SMS to {Mobile}", mobileNo);
            return false;
        }
    }
}
