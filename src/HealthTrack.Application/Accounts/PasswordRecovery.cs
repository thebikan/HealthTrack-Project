using System.Net.Mail;
using HealthTrack.Application.Security;

namespace HealthTrack.Application.Accounts;

public sealed record RecoveryRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public interface IPasswordRecoveryGateway
{
    Task RequestAsync(string email, CancellationToken ct);
    Task<bool> ResetAsync(ResetPasswordRequest request, CancellationToken ct);
}
// A delivery adapter must send privately to the verified account destination; never log tokens.
public interface IPasswordRecoveryDelivery
{
    bool IsConfigured { get; }
    Task SendAsync(string email, string token, CancellationToken ct);
}
public sealed class PasswordRecoveryService(IPasswordRecoveryGateway gateway)
{
    public Task RequestAsync(RecoveryRequest request, CancellationToken ct = default) => gateway.RequestAsync(Email(request.Email), ct);
    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var email = Email(request.Email);
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096 || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 128)
            throw RequestFailure.Invalid("A reset token and a password of at most 128 characters are required.");
        if (!await gateway.ResetAsync(request with { Email = email }, ct))
            throw RequestFailure.Invalid("The reset request is invalid or expired, or the password does not meet requirements.");
    }
    private static string Email(string email)
    {
        email = Input.Text(email, 256, "Email");
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email) throw RequestFailure.Invalid("A valid email is required.");
        return email;
    }
}
