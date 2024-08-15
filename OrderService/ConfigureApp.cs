using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace OrderService;

public static class ConfigureApp
{
    public static async Task Configure(this WebApplication app)
    {
        app.UseExceptionHandler(ConfigureExceptionHandler);
        app.UseSerilogRequestLogging();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseHttpsRedirection();
        app.UseCors("AllowAllOrigins");
        app.MapEndpoints();
        app.MapHealthChecks();
        await app.EnsureDatabaseCreated();
    }

    private static void ConfigureExceptionHandler(IApplicationBuilder errorApp)
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (errorFeature != null)
            {
                Log.Error(errorFeature.Error, "An error occurred while processing the request.");
                await context.Response.WriteAsync(new
                {
                    StatusCode = context.Response.StatusCode,
                    Message = "Internal Server Error"
                }.ToString() ?? string.Empty);
            }
        });
    }

    private static async Task EnsureDatabaseCreated(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        try
        {
            await db.Database.MigrateAsync();
            Log.Debug("Database migrated successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database migration failed.");
            throw;
        }
    }
    
    private static void MapHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false // Only include the 'liveness' checks
        });

        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("mq") // Include readiness checks for database and RabbitMQ
        });
    }
}