namespace HealthTrack.IntegrationTests.SqlServer;

public sealed class SqlTestGuardTests
{
    private const string Approval = "I_APPROVE_SYNTHETIC_WRITES";
    // Deliberately synthetic connection used only for parsing; never opened.
    private const string Connection = "Server=invalid.example;Database=HealthTrack_Integration_synthetic;Encrypt=True;TrustServerCertificate=False";

    [Fact]
    public void Missing_approval_is_rejected_before_connecting() =>
        Assert.Throws<InvalidOperationException>(() => SqlTestGuard.Validate(Connection, "HealthTrack_Integration_synthetic", null));

    [Theory]
    [InlineData("HealthTrack_Production")]
    [InlineData("HealthTrack_Integration_other")]
    [InlineData(null)]
    public void Wrong_database_is_rejected(string? expected) =>
        Assert.Throws<InvalidOperationException>(() => SqlTestGuard.Validate(Connection, expected, Approval));

    [Theory]
    [InlineData(";TrustServerCertificate=True")]
    [InlineData(";Encrypt=False")]
    [InlineData(";Persist Security Info=True")]
    public void Insecure_connection_is_rejected(string suffix) =>
        Assert.Throws<InvalidOperationException>(() => SqlTestGuard.Validate(Connection + suffix, "HealthTrack_Integration_synthetic", Approval));

    [Fact]
    public void Exact_test_target_with_verified_tls_is_accepted_without_connecting() =>
        Assert.Contains("HealthTrack.SqlIntegration", SqlTestGuard.Validate(Connection, "HealthTrack_Integration_synthetic", Approval));
}
