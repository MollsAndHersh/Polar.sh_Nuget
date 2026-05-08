using PolarSharp.Telemetry;

namespace PolarSharp.Tests.Telemetry;

public class PolarPiiRedactorTests
{
    // ── Redact() guard clause ─────────────────────────────────────────────

    [Fact]
    public void Redact_NullScope_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PolarPiiRedactor.Redact(null!));
    }

    [Fact]
    public void Redact_EmptyScope_ReturnsEmpty()
    {
        var result = PolarPiiRedactor.Redact(new Dictionary<string, object?>());
        Assert.Empty(result);
    }

    // ── Email masking ─────────────────────────────────────────────────────

    [Fact]
    public void Redact_CustomerEmail_MasksLocalPart()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_email"] = "john.doe@example.com" };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("j***@example.com", result["polar.customer_email"]);
    }

    [Fact]
    public void Redact_CustomerEmail_SingleCharLocal_MasksCorrectly()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_email"] = "a@b.com" };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("a***@b.com", result["polar.customer_email"]);
    }

    [Fact]
    public void Redact_CustomerEmail_MissingAtSign_ReturnsSentinel()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_email"] = "invalidemail" };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("***", result["polar.customer_email"]);
    }

    [Fact]
    public void Redact_CustomerEmail_NullValue_PassesThrough()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_email"] = null };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Null(result["polar.customer_email"]);
    }

    [Fact]
    public void Redact_CustomerEmail_NonStringValue_PassesThrough()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_email"] = 42 };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal(42, result["polar.customer_email"]);
    }

    // ── Name → initials ───────────────────────────────────────────────────

    [Fact]
    public void Redact_CustomerName_SingleName_ReturnsInitialWithDot()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_name"] = "Alice" };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("A.", result["polar.customer_name"]);
    }

    [Fact]
    public void Redact_CustomerName_FullName_ReturnsInitials()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_name"] = "John Smith" };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("J.S.", result["polar.customer_name"]);
    }

    [Fact]
    public void Redact_CustomerName_ThreePartName_ReturnsThreeInitials()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_name"] = "Anne Marie Jones" };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("A.M.J.", result["polar.customer_name"]);
    }

    [Fact]
    public void Redact_CustomerName_NullValue_PassesThrough()
    {
        var scope = new Dictionary<string, object?> { ["polar.customer_name"] = null };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Null(result["polar.customer_name"]);
    }

    // ── Error detail truncation ───────────────────────────────────────────

    [Fact]
    public void Redact_ErrorDetail_ShortValue_NotTruncated()
    {
        var short_detail = "request failed";
        var scope = new Dictionary<string, object?> { ["polar.error_detail"] = short_detail };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal(short_detail, result["polar.error_detail"]);
    }

    [Fact]
    public void Redact_ErrorDetail_LongValue_Truncated()
    {
        var longDetail = new string('x', 300);
        var scope = new Dictionary<string, object?> { ["polar.error_detail"] = longDetail };
        var result = PolarPiiRedactor.Redact(scope);
        var redacted = Assert.IsType<string>(result["polar.error_detail"]);
        Assert.True(redacted.Length < longDetail.Length);
        Assert.EndsWith("…", redacted);
    }

    [Fact]
    public void Redact_ErrorDetail_NonStringValue_PassesThrough()
    {
        var scope = new Dictionary<string, object?> { ["polar.error_detail"] = 404 };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal(404, result["polar.error_detail"]);
    }

    // ── IP hashing ────────────────────────────────────────────────────────

    [Fact]
    public void Redact_SourceIp_ProducesHashPrefix()
    {
        var scope = new Dictionary<string, object?> { ["polar.source_ip"] = "192.168.1.1" };
        var result = PolarPiiRedactor.Redact(scope);
        var hashed = Assert.IsType<string>(result["polar.source_ip"]);
        Assert.StartsWith("sha256:", hashed);
        Assert.Equal(15, hashed.Length); // "sha256:" (7) + 8 hex chars
    }

    [Fact]
    public void Redact_RemoteIp_ProducesHashPrefix()
    {
        var scope = new Dictionary<string, object?> { ["polar.remote_ip"] = "10.0.0.1" };
        var result = PolarPiiRedactor.Redact(scope);
        var hashed = Assert.IsType<string>(result["polar.remote_ip"]);
        Assert.StartsWith("sha256:", hashed);
    }

    [Fact]
    public void Redact_SourceIp_NullValue_ReturnsNull()
    {
        var scope = new Dictionary<string, object?> { ["polar.source_ip"] = null };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Null(result["polar.source_ip"]);
    }

    [Fact]
    public void Redact_SourceIp_SameIp_ProducesSameHash()
    {
        var ip = "1.2.3.4";
        var hash1 = PolarPiiRedactor.HashIp(ip);
        var hash2 = PolarPiiRedactor.HashIp(ip);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Redact_SourceIp_DifferentIps_ProduceDifferentHashes()
    {
        var hash1 = PolarPiiRedactor.HashIp("1.2.3.4");
        var hash2 = PolarPiiRedactor.HashIp("5.6.7.8");
        Assert.NotEqual(hash1, hash2);
    }

    // ── Non-PII keys pass through unchanged ───────────────────────────────

    [Fact]
    public void Redact_NonPiiKey_PassesThrough()
    {
        var scope = new Dictionary<string, object?>
        {
            ["polar.order_id"]    = "ord_123",
            ["polar.tenant_id"]   = "tenant-a",
            ["polar.http_status"] = 200,
        };
        var result = PolarPiiRedactor.Redact(scope);
        Assert.Equal("ord_123", result["polar.order_id"]);
        Assert.Equal("tenant-a", result["polar.tenant_id"]);
        Assert.Equal(200, result["polar.http_status"]);
    }

    // ── Mixed scope ───────────────────────────────────────────────────────

    [Fact]
    public void Redact_MixedScope_RedactsOnlyPiiKeys()
    {
        var scope = new Dictionary<string, object?>
        {
            ["polar.customer_email"] = "alice@example.com",
            ["polar.order_id"]       = "ord_abc",
            ["polar.source_ip"]      = "203.0.113.5",
        };
        var result = PolarPiiRedactor.Redact(scope);

        Assert.Equal("a***@example.com", result["polar.customer_email"]);
        Assert.Equal("ord_abc", result["polar.order_id"]);
        Assert.StartsWith("sha256:", result["polar.source_ip"] as string ?? "");
    }

    // ── HashIp standalone ─────────────────────────────────────────────────

    [Fact]
    public void HashIp_NullInput_ReturnsNull()
    {
        Assert.Null(PolarPiiRedactor.HashIp(null));
    }

    [Fact]
    public void HashIp_EmptyInput_ReturnsNull()
    {
        Assert.Null(PolarPiiRedactor.HashIp(""));
    }

    [Fact]
    public void HashIp_ValidIp_ReturnsExpectedFormat()
    {
        var result = PolarPiiRedactor.HashIp("127.0.0.1");
        Assert.NotNull(result);
        Assert.Matches(@"^sha256:[0-9a-f]{8}$", result);
    }
}
