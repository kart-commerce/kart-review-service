using Kart.Review.Api;
using Kart.Review.Api.Endpoints;
using Kart.Review.Api.HealthChecks;
using Kart.Review.Api.Security;
using Kart.Review.Application;
using Kart.Review.Application.Common.Exceptions;
using Kart.Review.Infrastructure;
using Kart.Review.Infrastructure.Auditing;
using Kart.Review.Infrastructure.Seeding;
using Kart.Shared.Auditing;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service — never reimplemented per service. Must run before
// AddKartObservability below, since observability's own LogFile:Directory setting is read from
// the layered-in GlobalConfig file too.
builder.AddKartGlobalConfig("kart-review-service");

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
// Review is not an Order-Saga participant, so the SDK's default sampling tier applies (not the
// 100%-trace-coverage tier reserved for kart-order-service/kart-inventory-service/
// kart-payment-service/kart-shipping-service).
builder.AddKartObservability("kart-review-service");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddReviewAuthentication();
builder.Services.AddAuthorization();

// kart-conventions.md Error Handling section: the single global exception handler + ProblemDetails
// factory, wired once via the shared package — no local try/catch for translation anywhere in
// this service's handler/endpoint/domain code.
builder.Services.AddKartErrorHandling(options => options
    .Map<ReviewNotFoundException>(StatusCodes.Status404NotFound, "review_not_found")
    .Map<ProductRatingNotFoundException>(StatusCodes.Status404NotFound, "product_rating_not_found")
    .Map<NotReviewAuthorException>(StatusCodes.Status403Forbidden, "not_review_author")
    .Map<VerifiedPurchaseNotFoundException>(StatusCodes.Status409Conflict, "verified_purchase_not_found")
    .Map<DuplicateReviewException>(StatusCodes.Status409Conflict, "duplicate_review")
    .Map<EditWindowClosedException>(StatusCodes.Status409Conflict, "edit_window_closed")
    .Map<ReviewTerminalStateException>(StatusCodes.Status409Conflict, "review_terminal_state")
    .Map<IdempotencyConflictException>(StatusCodes.Status422UnprocessableEntity, "idempotency_conflict"));

// Review's moderation actions and every submit/edit/retract mutation get a real, DB-backed audit
// trail — not the NullAuditLogWriter default (kart-requirements.md §24.3).
builder.Services.AddKartAuditing<EfAuditLogWriter>();

builder.Services.AddHealthChecks()
    .AddCheck<ReviewDbHealthCheck>("postgres", tags: ["ready"])
    .AddCheck<ReviewReadModelHealthCheck>("mongo", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Per-HTTP-request Information log (method/path/status/elapsed) — registered outermost, wrapping
// UseKartErrorHandling below, so this always logs the *final* status code a client actually
// received.
app.UseSerilogRequestLogging();

// The single global error handler — every unhandled exception is translated to the platform's
// ProblemDetails envelope and logged here.
app.UseKartErrorHandling();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Prometheus scrape target (observability-standards.md's mandatory /metrics).
app.MapPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });

app.MapReviewEndpoints();
app.MapProductRatingEndpoints();

await DevDataSeeder.SeedAsync(app.Services, app.Configuration, CancellationToken.None);
await StartupConnectivityChecks.RunAsync(app);

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program
{
}
