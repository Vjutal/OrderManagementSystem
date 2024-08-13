using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RabbitMQ.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/userservice-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://localhost:5341")  // Optional, if you want to use Seq for structured logging
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigins",
        builder =>
        {
            builder.WithOrigins("*")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// Configure services
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<RabbitMQPublisher>();

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"), 
        name: "postgresql", 
        tags: new[] { "db", "sql", "postgresql" })
    .AddRabbitMQ(builder.Configuration.GetConnectionString("RabbitMQConnection"), 
        name: "rabbitmq", 
        tags: new[] { "mq", "rabbitmq" });

// Configure API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Add services to the container.
var app = builder.Build();

// Global Exception Handler
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

// Enforce HTTPS
app.UseHttpsRedirection();

app.UseCors("AllowAllOrigins");

// Define endpoints
app.MapGet("/users", async (UserDbContext db) =>
{
    Log.Information("Getting all users");
    return Results.Ok(await db.Users.ToListAsync());
})
.WithMetadata(new ApiVersionAttribute("1.0"));

app.MapGet("/users/{id:int}", async (int id, UserDbContext db) =>
{
    Log.Information("Getting user with ID: {UserId}", id);
    var user = await db.Users.FindAsync(id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithMetadata(new ApiVersionAttribute("1.0"));

app.MapPost("/users", async (User user, UserDbContext db, RabbitMQPublisher publisher) =>
{
    Log.Information("Creating a new user: {Email}", user.Email);
    db.Users.Add(user);
    await db.SaveChangesAsync();

    await publisher.PublishUserCreatedAsync(user);

    Log.Information("User created with ID: {UserId} and published to RabbitMQ", user.Id);
    return Results.Created($"/users/{user.Id}", user);
})
.WithMetadata(new ApiVersionAttribute("1.0"));

app.MapPut("/users/{id:int}", async (int id, User updatedUser, UserDbContext db) =>
{
    Log.Information("Updating user with ID: {UserId}", id);
    var user = await db.Users.FindAsync(id);
    if (user is null)
    {
        Log.Warning("User with ID: {UserId} not found", id);
        return Results.NotFound();
    }

    user.FirstName = updatedUser.FirstName;
    user.LastName = updatedUser.LastName;
    user.Email = updatedUser.Email;

    await db.SaveChangesAsync();
    Log.Information("User with ID: {UserId} updated successfully", id);
    return Results.NoContent();
})
.WithMetadata(new ApiVersionAttribute("1.0"));

app.MapDelete("/users/{id:int}", async (int id, UserDbContext db) =>
{
    Log.Information("Deleting user with ID: {UserId}", id);
    var user = await db.Users.FindAsync(id);
    if (user is not null)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        Log.Information("User with ID: {UserId} deleted successfully", id);
        return Results.NoContent();
    }
    else
    {
        Log.Warning("User with ID: {UserId} not found", id);
        return Results.NotFound();
    }
})
.WithMetadata(new ApiVersionAttribute("1.0"));

// Map Health Check Endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Only include the 'liveness' checks
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("mq") // Include readiness checks for database and RabbitMQ
});

// Run the application
try
{
    Log.Information("Starting UserService");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "UserService failed to start.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public record User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; }

    [Required]
    [StringLength(50)]
    public string LastName { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class UserDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
    {
    }
}

public class RabbitMQPublisher : IDisposable
    {
        private readonly IModel _channel;
        private readonly IConnection _connection;
        private readonly int _retryCount;

        public RabbitMQPublisher(IConfiguration configuration)
        {
            var factory = new ConnectionFactory()
            {
                Uri = new Uri(configuration.GetConnectionString("RabbitMQConnection"))
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: "user_queue",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            _retryCount = configuration.GetValue<int>("RabbitMQPublisherSettings:RetryCount");
        }

        public async Task PublishUserCreatedAsync(User user)
        {
            var message = JsonSerializer.Serialize(user);
            var body = Encoding.UTF8.GetBytes(message);

            var retries = _retryCount;
            while (retries > 0)
            {
                try
                {
                    _channel.BasicPublish(exchange: "",
                                          routingKey: "user_queue",
                                          basicProperties: null,
                                          body: body);
                    Log.Information("User published to RabbitMQ: {UserId}", user.Id);
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to publish message, retrying...");
                    retries--;
                    if (retries == 0)
                    {
                        Log.Error(ex, "Failed to publish message after retries: {UserId}", user.Id);
                        throw;
                    }

                    await Task.Delay(1000); // Wait before retrying
                }
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
    
public class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UserDbContext>();

        // Build configuration
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        optionsBuilder.UseNpgsql(connectionString);

        return new UserDbContext(optionsBuilder.Options);
    }
}