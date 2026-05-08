using PolarSharp.Extensions;
using PolarSharp.Webhooks.Extensions;
using PolarSharp.MultiTenant.Extensions;
using PolarTestApp.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPolarInfrastructure(builder.Configuration)
    .AddPolarWebhooks()
    .AddPolarMultiTenant();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseRequestLocalization(opts =>
    opts.SetDefaultCulture("en-US")
        .AddSupportedCultures("en-US", "es-MX")
        .AddSupportedUICultures("en-US", "es-MX"));

app.UsePolarInfrastructure();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapOrderEndpoints()
   .MapSubscriptionEndpoints()
   .MapCustomerEndpoints()
   .MapProductEndpoints()
   .MapCheckoutEndpoints()
   .MapBenefitEndpoints()
   .MapLicenseKeyEndpoints()
   .MapMeterEndpoints()
   .MapDiscountEndpoints()
   .MapRefundEndpoints()
   .MapWebhookSimulatorEndpoints()
   .MapMultiTenantEndpoints()
   .MapLocalizationEndpoints()
   .MapDiagnosticsEndpoints()
   .MapCustomerPortalEndpoints();

app.Run();
