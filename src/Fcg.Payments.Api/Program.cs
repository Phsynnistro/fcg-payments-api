using Fcg.Payments.Api.Application;
using Fcg.Payments.Api.Infrastructure;
using Fcg.Payments.Api.Infrastructure.Persistence;
using Fcg.Payments.Api.Presentation;
using Fcg.Payments.Api.Presentation.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiDefaults()
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiDocs("FCG • Payments API");
app.MapHealthEndpoints();
app.MapPaymentEndpoints();

await app.Services.InitializeDatabaseAsync<PaymentsDbContext>(app.Lifetime.ApplicationStopping);
await app.RunAsync();
