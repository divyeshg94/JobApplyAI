using JobApplyAi.Domain;
using JobApplyAi.Domain.Entities;
using JobApplyAi.Domain.Seed;
using JobApplyAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobApplyAi.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public record SubscriptionRequest(JobSource Source, string DisplayName, string ConfigJson, bool IsEnabled = true);

    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscriptions");

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            await db.JobSourceSubscriptions
                .Where(s => s.UserId == SeedData.DefaultUserId)
                .OrderBy(s => s.DisplayName)
                .ToListAsync(ct));

        group.MapPost("/", async (SubscriptionRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var subscription = new JobSourceSubscription
            {
                Id = Guid.NewGuid(),
                UserId = SeedData.DefaultUserId,
                Source = request.Source,
                DisplayName = request.DisplayName,
                ConfigJson = request.ConfigJson,
                IsEnabled = request.IsEnabled,
            };
            db.JobSourceSubscriptions.Add(subscription);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/subscriptions/{subscription.Id}", subscription);
        });

        group.MapPut("/{id:guid}", async (Guid id, SubscriptionRequest request, AppDbContext db, CancellationToken ct) =>
        {
            var subscription = await db.JobSourceSubscriptions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == SeedData.DefaultUserId, ct);
            if (subscription is null)
            {
                return Results.NotFound();
            }

            subscription.Source = request.Source;
            subscription.DisplayName = request.DisplayName;
            subscription.ConfigJson = request.ConfigJson;
            subscription.IsEnabled = request.IsEnabled;
            await db.SaveChangesAsync(ct);
            return Results.Ok(subscription);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var subscription = await db.JobSourceSubscriptions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == SeedData.DefaultUserId, ct);
            if (subscription is null)
            {
                return Results.NotFound();
            }

            db.JobSourceSubscriptions.Remove(subscription);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}
