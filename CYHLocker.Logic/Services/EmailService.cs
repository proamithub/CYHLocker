using CYHLocker.Logic.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CYHLocker.Logic.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService>? _logger;

    public EmailService(IConfiguration config, ILogger<EmailService>? logger = null)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, string resetUrl)
    {
        var subject = "CYHLocker — Master Password Reset Request";
        var bodyHtml = $@"
            <div style=""background:#070b16; color:#f8fafc; font-family:'Segoe UI',sans-serif; padding:30px; border-radius:12px; max-width:550px; margin:auto; border:1px solid rgba(6,182,212,0.3);"">
                <div style=""text-align:center; margin-bottom:20px;"">
                    <span style=""font-size:2rem;"">🔒</span>
                    <h2 style=""color:#06b6d4; margin:5px 0 0;"">CYHLocker Security</h2>
                </div>
                <p>Hello <strong>{fullName}</strong>,</p>
                <p>We received a request to reset your Master Password. Click the button below to establish a new password for your encrypted vault:</p>
                <div style=""text-align:center; margin:30px 0;"">
                    <a href=""{resetUrl}"" style=""background:linear-gradient(135deg, #06b6d4, #0284c7); color:#04101c; font-weight:bold; padding:12px 28px; text-decoration:none; border-radius:8px; display:inline-block;"">Reset Master Password</a>
                </div>
                <p style=""font-size:0.85rem; color:#94a3b8;"">If the button doesn't work, copy this link into your browser:<br/><a href=""{resetUrl}"" style=""color:#06b6d4;"">{resetUrl}</a></p>
                <p style=""font-size:0.8rem; color:#64748b; margin-top:25px; border-top:1px solid rgba(255,255,255,0.1); padding-top:15px;"">
                    If you did not request this, please ignore this email. Your vault remains safely encrypted.
                </p>
            </div>";

        return await SendAsync(toEmail, fullName, subject, bodyHtml);
    }

    public async Task<bool> SendAdminEmergencyRecoveryEmailAsync(string adminEmail, string recoveryOtp, string actionDetails)
    {
        var subject = "⚠️ CYHLocker Emergency Super Admin Action Required";
        var bodyHtml = $@"
            <div style=""background:#0f172a; color:#f8fafc; font-family:'Segoe UI',sans-serif; padding:30px; border-radius:12px; max-width:550px; margin:auto; border:1px solid rgba(239,68,68,0.4);"">
                <h2 style=""color:#f87171; margin-top:0;"">⚠️ Emergency Admin Verification</h2>
                <p>An emergency account recovery / unlock action has been requested:</p>
                <div style=""background:rgba(239,68,68,0.1); border:1px solid rgba(239,68,68,0.3); padding:10px 15px; border-radius:8px; margin:15px 0;"">
                    <strong>Action:</strong> {actionDetails}
                </div>
                <p>Your one-time authorization token is:</p>
                <div style=""background:#1e293b; padding:15px; font-family:monospace; font-size:1.5rem; text-align:center; color:#38bdf8; letter-spacing:0.2em; border-radius:8px; font-weight:bold;"">
                    {recoveryOtp}
                </div>
                <p style=""font-size:0.85rem; color:#94a3b8; margin-top:20px;"">This token is strictly confidential and expires in 15 minutes.</p>
            </div>";

        return await SendAsync(adminEmail, "Super Admin", subject, bodyHtml);
    }

    public async Task<bool> SendSubscriptionWelcomeEmailAsync(string toEmail, string fullName, string planName, decimal amountINR)
    {
        var subject = "🎉 Welcome to CYHLocker Pro — Subscription Activated";
        var bodyHtml = $@"
            <div style=""background:#070b16; color:#f8fafc; font-family:'Segoe UI',sans-serif; padding:30px; border-radius:12px; max-width:550px; margin:auto; border:1px solid rgba(16,185,129,0.3);"">
                <div style=""text-align:center; margin-bottom:20px;"">
                    <span style=""font-size:2.5rem;"">💎</span>
                    <h2 style=""color:#34d399; margin:5px 0 0;"">Subscription Confirmed!</h2>
                </div>
                <p>Hi <strong>{fullName}</strong>,</p>
                <p>Thank you for choosing CYHLocker. Your account has been upgraded to <strong>{planName}</strong> for ₹{amountINR:F2}.</p>
                <ul style=""color:#94a3b8; line-height:1.8;"">
                    <li>✓ Unlimited Logins & Password Generator</li>
                    <li>✓ Unlimited Payment Cards & Secure Documents</li>
                    <li>✓ Multi-Device Cloud Sync Enabled</li>
                    <li>✓ 24/7 Priority Encryption Shield</li>
                </ul>
                <p style=""font-size:0.8rem; color:#64748b; margin-top:25px; border-top:1px solid rgba(255,255,255,0.1); padding-top:15px;"">
                    Your invoice and receipt have been registered with Razorpay Payments.
                </p>
            </div>";

        return await SendAsync(toEmail, fullName, subject, bodyHtml);
    }

    private async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host = _config["Smtp:Host"];
        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user = _config["Smtp:Username"];
        var pass = _config["Smtp:Password"];
        var from = _config["Smtp:From"] ?? "security@cyhlocker.com";

        // If SMTP server is not yet configured, log to output safely without throwing
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
        {
            _logger?.LogInformation("[CYH EMAIL SIMULATION] To: {Email} | Subject: {Subject}", toEmail, subject);
            return true;
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress("CYHLocker Security", from));
            msg.To.Add(new MailboxAddress(toName, toEmail));
            msg.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = htmlBody };
            msg.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user, pass);
            await client.SendAsync(msg);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[CYH] Failed to send email to {Email}", toEmail);
            return false;
        }
    }
}
