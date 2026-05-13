using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace PolarSharp.Onboarding.Wizard;

/// <summary>
/// EF Core model customizer — appends the <see cref="OnboardingSessionEntity"/> mapping to
/// the host's <see cref="DbContext"/> model.
/// </summary>
/// <remarks>
/// Registered via <c>opts.ReplaceService&lt;IModelCustomizer, OnboardingModelCustomizer&gt;()</c>
/// in the Onboarding wizard's DI registration. Inherits from
/// <see cref="RelationalModelCustomizer"/> so existing model conventions stay intact — we only
/// layer on the onboarding session entity.
/// </remarks>
internal sealed class OnboardingModelCustomizer : RelationalModelCustomizer
{
    public OnboardingModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies) { }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        base.Customize(modelBuilder, context);

        modelBuilder.Entity<OnboardingSessionEntity>(e =>
        {
            e.ToTable("polar_onboarding_sessions");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.ExpiresAt);
            e.Property(s => s.CompletedStepsJson).IsRequired();
            e.Property(s => s.AccumulatedDataJson).IsRequired();
            e.Property(s => s.FinishedTenantId).HasMaxLength(64);
        });
    }
}
