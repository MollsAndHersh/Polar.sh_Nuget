using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PolarSharp;
using PolarSharp.MultiTenant.EntityFrameworkCore;
using PolarSharp.Onboarding;
using PolarSharp.Onboarding.Wizard;

namespace PolarSharp.Onboarding.Tests;

/// <summary>
/// Verifies wizard session lifecycle, conditional next-step logic, API-key encryption at
/// rest, expiration handling, and finish→programmatic-API delegation.
/// </summary>
public sealed class OnboardingWizardTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public OnboardingWizardTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        services.AddDbContext<PolarTenantDbContext>(opts =>
            opts.UseSqlite(_connection)
                .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, OnboardingModelCustomizer>());

        services.AddOptions<PolarOnboardingOptions>().Configure(o => o.Wizard.Enabled = true);

        services.AddScoped<IPolarOnboardingApi, FakePolarOnboardingApi>();
        services.AddSingleton<RecordingSink>();
        services.AddScoped<IOnboardedTenantSink>(sp => sp.GetRequiredService<RecordingSink>());
        services.AddScoped<IPolarOnboardingClient, PolarOnboardingClient>();
        services.AddScoped<IOnboardingWizard, OnboardingWizard>();

        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() { _services.Dispose(); _connection.Dispose(); }

    [Fact]
    public async Task Start_creates_persistent_session_row_with_expiration()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.True(session.ExpiresAt > session.CreatedAt);

        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        var row = await db.Set<OnboardingSessionEntity>().FirstAsync(s => s.Id == session.Id);
        Assert.False(row.IsFinished);
        Assert.False(row.IsCanceled);
    }

    [Fact]
    public async Task Conditional_next_step_skips_TranslationConfig_when_multi_language_is_false()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();

        await wizard.SubmitCompanyBasicsAsync(session.Id, ValidCompanyBasics());
        var productResult = await wizard.SubmitProductTypesAsync(session.Id, new ProductTypesStep
        {
            SellsPhysicalGoods = true,
            SellsDigitalGoods = false,
            SellsServices = false,
            SellsSubscriptions = false,
            RequiresLicenseKeys = false,
            RequiresFileDownloads = false,
            RequiresMultiLanguage = false,
        });

        var step = productResult.Match<OnboardingStepResult?>(s => s, _ => null);
        Assert.NotNull(step);
        Assert.Equal(OnboardingStepKind.WebhookConfig, step.NextStep);
        Assert.DoesNotContain(OnboardingStepKind.TranslationConfig, step.RemainingSteps);
    }

    [Fact]
    public async Task Conditional_next_step_includes_TranslationConfig_when_multi_language_is_true()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();

        await wizard.SubmitCompanyBasicsAsync(session.Id, ValidCompanyBasics());
        var productResult = await wizard.SubmitProductTypesAsync(session.Id, new ProductTypesStep
        {
            SellsPhysicalGoods = false,
            SellsDigitalGoods = false,
            SellsServices = false,
            SellsSubscriptions = true,
            RequiresLicenseKeys = false,
            RequiresFileDownloads = false,
            RequiresMultiLanguage = true,
        });

        var step = productResult.Match<OnboardingStepResult?>(s => s, _ => null);
        Assert.NotNull(step);
        Assert.Contains(OnboardingStepKind.TranslationConfig, step.RemainingSteps);
    }

    [Fact]
    public async Task TranslationConfig_API_key_is_encrypted_at_rest_in_session_blob()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();
        const string plaintextKey = "sk-ant-PLAINTEXT-SHOULD-NOT-APPEAR-IN-DB";

        await wizard.SubmitTranslationConfigAsync(session.Id, new TranslationConfigStep
        {
            Provider = "Anthropic",
            ApiKeyPlaintext = plaintextKey,
            Model = "claude-sonnet-4-6",
            MasterLanguage = "en-US",
            SupportedLanguages = ["es-MX", "fr-FR"],
        });

        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        var row = await db.Set<OnboardingSessionEntity>().FirstAsync(s => s.Id == session.Id);

        Assert.DoesNotContain(plaintextKey, row.AccumulatedDataJson);

        // Confirm the encrypted value can be unprotected back to the original.
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("PolarSharp.Onboarding.Wizard.TranslationApiKey");
        var accumulated = JsonSerializer.Deserialize<Dictionary<OnboardingStepKind, JsonElement>>(
            row.AccumulatedDataJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var encrypted = accumulated![OnboardingStepKind.TranslationConfig]
            .Deserialize<TranslationConfigStep>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!
            .ApiKeyPlaintext;
        Assert.NotNull(encrypted);
        Assert.NotEqual(plaintextKey, encrypted);
        Assert.Equal(plaintextKey, protector.Unprotect(encrypted));
    }

    [Fact]
    public async Task Resume_via_GetCurrentStateAsync_returns_completed_and_remaining_steps()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();
        await wizard.SubmitCompanyBasicsAsync(session.Id, ValidCompanyBasics());

        var state = await wizard.GetCurrentStateAsync(session.Id);

        var summary = state.Match<OnboardingSessionSummary?>(s => s, _ => null);
        Assert.NotNull(summary);
        Assert.Equal(OnboardingStepKind.CompanyBasics, summary.CurrentStep);
        Assert.Contains(OnboardingStepKind.ProductTypes, summary.RemainingSteps);
    }

    [Fact]
    public async Task FinishAsync_with_remaining_steps_returns_StepOutOfOrder_error()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();
        await wizard.SubmitCompanyBasicsAsync(session.Id, ValidCompanyBasics());

        var result = await wizard.FinishAsync(session.Id);

        Assert.False(result.IsSuccess);
        var err = result.Match<OnboardingError?>(_ => null, e => e);
        Assert.NotNull(err);
        Assert.Equal(OnboardingErrorKind.WizardStepOutOfOrder, err.Kind);
    }

    [Fact]
    public async Task FinishAsync_after_all_steps_invokes_programmatic_API_and_marks_finished()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();
        var api = (FakePolarOnboardingApi)scope.ServiceProvider.GetRequiredService<IPolarOnboardingApi>();
        var sink = scope.ServiceProvider.GetRequiredService<RecordingSink>();

        var session = await wizard.StartAsync();
        await wizard.SubmitCompanyBasicsAsync(session.Id, ValidCompanyBasics());
        await wizard.SubmitProductTypesAsync(session.Id, new ProductTypesStep
        {
            SellsPhysicalGoods = false, SellsDigitalGoods = false, SellsServices = false,
            SellsSubscriptions = false, RequiresLicenseKeys = false, RequiresFileDownloads = false,
            RequiresMultiLanguage = false,
        });
        await wizard.SubmitWebhookConfigAsync(session.Id, new WebhookConfigStep
        {
            CallbackUrl = "https://app.example.com/hooks/polar",
            EventTypes = ["order.created"],
        });
        await wizard.SubmitBankingHandoffAsync(session.Id, new BankingHandoffStep
        {
            AcknowledgedDashboardRedirect = true,
        });

        var result = await wizard.FinishAsync(session.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(api.CreateOrgCalls);
        Assert.Single(sink.Persisted);

        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        var row = await db.Set<OnboardingSessionEntity>().FirstAsync(s => s.Id == session.Id);
        Assert.True(row.IsFinished);
        Assert.NotNull(row.FinishedTenantId);
    }

    [Fact]
    public async Task Cancel_idempotently_marks_session_as_cancelled()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();
        await wizard.CancelAsync(session.Id);
        await wizard.CancelAsync(session.Id);   // idempotent

        var state = await wizard.GetCurrentStateAsync(session.Id);
        Assert.False(state.IsSuccess);   // a cancelled session is no longer queryable as active
    }

    [Fact]
    public async Task Submitting_step_to_cancelled_session_returns_SessionInvalid()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();
        await wizard.CancelAsync(session.Id);

        var result = await wizard.SubmitCompanyBasicsAsync(session.Id, ValidCompanyBasics());

        var err = result.Match<OnboardingError?>(_ => null, e => e);
        Assert.NotNull(err);
        Assert.Equal(OnboardingErrorKind.WizardSessionInvalid, err.Kind);
    }

    [Fact]
    public async Task Invalid_company_basics_returns_validation_errors_without_advancing_steps()
    {
        using var scope = _services.CreateScope();
        var wizard = scope.ServiceProvider.GetRequiredService<IOnboardingWizard>();

        var session = await wizard.StartAsync();
        var result = await wizard.SubmitCompanyBasicsAsync(session.Id, new CompanyBasicsStep
        {
            OrganizationName = "",
            OrganizationSlug = "",
            Email = "not-an-email",
            CountryCode = "USA",     // wrong length
            Currency = "US",         // wrong length
            PrimaryAdminEmail = "no-at-sign",
        });

        var step = result.Match<OnboardingStepResult?>(s => s, _ => null);
        Assert.NotNull(step);
        Assert.False(step.IsValid);
        Assert.NotEmpty(step.ValidationErrors);

        var db = scope.ServiceProvider.GetRequiredService<PolarTenantDbContext>();
        var row = await db.Set<OnboardingSessionEntity>().FirstAsync(s => s.Id == session.Id);
        Assert.Equal("[]", row.CompletedStepsJson);   // no advancement on invalid submission
    }

    private static CompanyBasicsStep ValidCompanyBasics() => new()
    {
        OrganizationName = "Acme",
        OrganizationSlug = "acme",
        Email = "ops@acme.example.com",
        CountryCode = "US",
        Currency = "USD",
        PrimaryAdminEmail = "admin@acme.example.com",
    };
}
