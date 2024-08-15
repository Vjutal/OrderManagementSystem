using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Shared;

public static class LoggingExtensions
{
    public static IHostBuilder ConfigureLogging(this IHostBuilder hostBuilder, IConfiguration config)
    {
        hostBuilder.UseSerilog((context, services, configuration) =>
        {
            configuration.ReadFrom.Configuration(config);
            
            configuration
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:l}{NewLine}{Exception}")
                .WriteTo.File("logs/service-.log", rollingInterval: RollingInterval.Day)
                .WriteTo.Seq("http://localhost:5341")  // Optional, if you want to use Seq for structured logging
                .Enrich.FromLogContext();
        });

        return hostBuilder;
    }
}

public static class CorsExtensions
{
    public static void AddDefaultCorsPolicy(this IServiceCollection services, string policyName)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, builder =>
            {
                builder.WithOrigins("https://www.example.com") // Replace with your frontend's URL
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
}
    
public static class HealthCheckExtensions
{
    public static void AddDefaultHealthChecks(this IServiceCollection services, string postgresConnectionString, string rabbitMqConnectionString)
    {
        services.AddHealthChecks()
            .AddNpgSql(postgresConnectionString, 
                name: "postgresql", 
                tags: new[] { "db", "sql", "postgresql" })
            .AddRabbitMQ(rabbitMqConnectionString, 
                name: "rabbitmq", 
                tags: new[] { "mq", "rabbitmq" });
    }
}
    
public static class ApiVersioningExtensions
{
    public static void AddDefaultApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });
    }
}
    
public static class DbContextExtensions
{
    public static void AddDefaultDbContext<TContext>(this IServiceCollection services, string connectionString) where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(connectionString));
    }
}

public static class ExceptionHandlerExtensions
{
    public static void UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp =>
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
                    }.ToString());
                }
            });
        });
    }
}

public class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().First();

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (result.IsValid) return await next(context);

        return TypedResults.ValidationProblem(result.ToDictionary());
    }
}

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithRequestValidation<TRequest>(this RouteHandlerBuilder builder)
    {
        return builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
    }
}