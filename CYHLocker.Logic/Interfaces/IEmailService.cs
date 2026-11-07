namespace CYHLocker.Logic.Interfaces;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, string resetUrl);
    Task<bool> SendAdminEmergencyRecoveryEmailAsync(string adminEmail, string recoveryOtp, string actionDetails);
    Task<bool> SendSubscriptionWelcomeEmailAsync(string toEmail, string fullName, string planName, decimal amountINR);
}
