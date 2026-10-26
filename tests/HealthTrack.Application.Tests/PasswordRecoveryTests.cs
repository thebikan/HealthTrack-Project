using HealthTrack.Application.Accounts;
using HealthTrack.Application.Security;

namespace HealthTrack.Application.Tests;
public sealed class PasswordRecoveryTests
{
    [Fact]
    public async Task Invalid_input_never_reaches_gateway()
    {
        var gateway = new Gateway(); var service = new PasswordRecoveryService(gateway);
        await Assert.ThrowsAsync<RequestFailure>(() => service.RequestAsync(new("not-an-email")));
        await Assert.ThrowsAsync<RequestFailure>(() => service.ResetAsync(new("synthetic@example.test", "", "Password42!")));
        Assert.Equal(0, gateway.Calls);
    }
    [Fact]
    public async Task Failed_reset_has_generic_error_without_token_or_email()
    {
        var error = await Assert.ThrowsAsync<RequestFailure>(() => new PasswordRecoveryService(new Gateway()).ResetAsync(new("synthetic@example.test", "private-token", "Password42!")));
        Assert.DoesNotContain("private-token", error.Message);
        Assert.DoesNotContain("synthetic", error.Message);
    }
    private sealed class Gateway : IPasswordRecoveryGateway
    {
        public int Calls;
        public Task RequestAsync(string email, CancellationToken ct) { Calls++; return Task.CompletedTask; }
        public Task<bool> ResetAsync(ResetPasswordRequest request, CancellationToken ct) { Calls++; return Task.FromResult(false); }
    }
}
