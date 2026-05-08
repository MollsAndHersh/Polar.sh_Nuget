using System.Text.Json;
using PolarSharp.Serialization;
using PolarSharp.Webhooks.Events;
using VerifyXunit;

namespace PolarSharp.Tests.Serialization;

/// <summary>
/// Locks down the JSON serialization shape of every public record type that crosses the wire.
/// A test failure means a property name, type, or structure changed — review the diff and either
/// accept it (promote .received.txt → .verified.txt and commit) or revert the unintentional change.
/// </summary>
public class JsonWireFormatTests : VerifyBase
{
    public JsonWireFormatTests() : base() { }

    // Write-indented options for readable snapshots.
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented              = true,
        DefaultIgnoreCondition     = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling        = JsonCommentHandling.Disallow,
        AllowTrailingCommas        = false,
        MaxDepth                   = 32,
    };

    // ── PolarError hierarchy ──────────────────────────────────────────────────

    [Fact]
    public Task AuthenticationError_SerializationShape()
    {
        var error = new AuthenticationError("Authentication failed.", "req_aaa");
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("AuthenticationError");
    }

    [Fact]
    public Task AuthorizationError_SerializationShape()
    {
        var error = new AuthorizationError("Access denied.", "req_bbb");
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("AuthorizationError");
    }

    [Fact]
    public Task NotFoundError_SerializationShape()
    {
        var error = new NotFoundError("Resource not found.", "req_ccc");
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("NotFoundError");
    }

    [Fact]
    public Task RateLimitError_SerializationShape()
    {
        var error = new RateLimitError("Rate limit exceeded.", "req_ddd", TimeSpan.FromSeconds(30));
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("RateLimitError");
    }

    [Fact]
    public Task ServerError_SerializationShape()
    {
        var error = new ServerError("Server error.", "req_eee");
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("ServerError");
    }

    [Fact]
    public Task ValidationError_SerializationShape()
    {
        var fields = new List<FieldValidationError>
        {
            new("email", "must be a valid email address"),
            new("amount", "must be greater than zero"),
        };
        var error = new ValidationError("Validation failed.", "req_fff", fields);
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("ValidationError");
    }

    [Fact]
    public Task FieldValidationError_SerializationShape()
    {
        var error = new FieldValidationError("email", "must be a valid email address");
        var json  = JsonSerializer.Serialize(error, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("FieldValidationError");
    }

    // ── Webhook event data types ───────────────────────────────────────────────

    [Fact]
    public Task WebhookCustomer_SerializationShape()
    {
        var customer = new WebhookCustomer
        {
            Id    = "cus_01jf4g9h3k2m",
            Email = "test@example.com",
            Name  = "Test User",
        };
        var json = JsonSerializer.Serialize(customer, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookCustomer");
    }

    [Fact]
    public Task WebhookOrderData_SerializationShape()
    {
        var data = new WebhookOrderData
        {
            Id            = "ord_01jf4g9h3k2m",
            Status        = "paid",
            Number        = "1001",
            Amount        = 2999,
            TaxAmount     = 300,
            Currency      = "USD",
            Channel       = "web",
            BillingReason = "purchase",
            Customer      = new WebhookCustomer { Id = "cus_01", Email = "buyer@example.com", Name = "Buyer" },
            Items         =
            [
                new WebhookOrderItem
                {
                    ProductId   = "prod_01",
                    ProductName = "Pro Plan",
                    PriceAmount = 2999,
                    Currency    = "USD",
                }
            ],
            CreatedAt = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookOrderData");
    }

    [Fact]
    public Task WebhookSubscriptionData_SerializationShape()
    {
        var data = new WebhookSubscriptionData
        {
            Id                 = "sub_01jf4g9h3k2m",
            Status             = "active",
            CurrentPeriodStart = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CurrentPeriodEnd   = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
            Customer           = new WebhookCustomer { Id = "cus_01", Email = "sub@example.com" },
            Product            = new WebhookProduct  { Id = "prod_01", Name = "Pro Plan" },
            Price              = new WebhookPrice     { Id = "price_01", Name = "Monthly", Amount = 2999, Currency = "USD" },
            CreatedAt          = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookSubscriptionData");
    }

    [Fact]
    public Task WebhookCheckoutData_SerializationShape()
    {
        var data = new WebhookCheckoutData
        {
            Id            = "chk_01jf4g9h3k2m",
            Status        = "confirmed",
            Amount        = 2999,
            Currency      = "USD",
            CustomerEmail = "checkout@example.com",
            Customer      = new WebhookCustomer { Id = "cus_01", Email = "checkout@example.com" },
            ExpiresAt     = new DateTimeOffset(2025, 1, 16, 10, 30, 0, TimeSpan.Zero),
            OrderId       = "ord_01",
            CreatedAt     = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookCheckoutData");
    }

    [Fact]
    public Task WebhookCustomerData_SerializationShape()
    {
        var data = new WebhookCustomerData
        {
            Id                        = "cus_01jf4g9h3k2m",
            Email                     = "customer@example.com",
            Name                      = "Jane Doe",
            OrganizationId            = "org_01",
            ActiveSubscriptionsCount  = 2,
            CreatedAt                 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookCustomerData");
    }

    [Fact]
    public Task WebhookProductData_SerializationShape()
    {
        var data = new WebhookProductData
        {
            Id             = "prod_01jf4g9h3k2m",
            Name           = "Pro Plan",
            IsArchived     = false,
            OrganizationId = "org_01",
            CreatedAt      = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookProductData");
    }

    [Fact]
    public Task WebhookBenefitData_SerializationShape()
    {
        var data = new WebhookBenefitData
        {
            Id             = "ben_01jf4g9h3k2m",
            BenefitType    = "license_keys",
            Description    = "Access to license key vault",
            OrganizationId = "org_01",
            CreatedAt      = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookBenefitData");
    }

    [Fact]
    public Task WebhookBenefitGrantData_SerializationShape()
    {
        var data = new WebhookBenefitGrantData
        {
            Id          = "bg_01jf4g9h3k2m",
            CustomerId  = "cus_01",
            BenefitId   = "ben_01",
            BenefitType = "license_keys",
            IsGranted   = true,
            GrantedAt   = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookBenefitGrantData");
    }

    [Fact]
    public Task WebhookRefundData_SerializationShape()
    {
        var data = new WebhookRefundData
        {
            Id         = "ref_01jf4g9h3k2m",
            Amount     = 2999,
            Currency   = "USD",
            Reason     = "customer_request",
            Status     = "succeeded",
            OrderId    = "ord_01",
            CustomerId = "cus_01",
            CreatedAt  = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero),
        };
        var json = JsonSerializer.Serialize(data, IndentedOptions);
        return Verify(json)
            .UseDirectory("JsonSnapshots")
            .UseFileName("WebhookRefundData");
    }
}
