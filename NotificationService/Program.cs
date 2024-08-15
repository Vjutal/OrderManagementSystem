using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables(prefix: "DOTNET_")
    .AddEnvironmentVariables(prefix: "ASPNETCORE_")
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true);


// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:l}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateLogger();

// builder.Host.UseSerilog();

// Configure EF Core with an in-memory database for simplicity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<OrderConsumer>();

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.SetSnakeCaseEndpointNameFormatter();
    
    x.AddConsumer<OrderConsumer>().Endpoint(c =>
    {
        // c.Name = "order_queue";
        // c.InstanceId = "notifcation_queue";
    });
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQConnection"));

        cfg.ReceiveEndpoint("notification_queue", e =>
        {
            e.Bind("order_queue");
            
            e.UseMessageRetry(c =>
                c.SetRetryPolicy(filter => 
                    filter.Interval(5, TimeSpan.FromSeconds(5))
                    ));
            
            e.ConfigureConsumer<OrderConsumer>(context);
            cfg.ConfigureEndpoints(context);

        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    try
    {
        db.Database.Migrate();
        Log.Information("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration failed.");
        throw;
    }
}

try
{
    Log.Information("Starting NotificationService");

    // This will start the message processing and keep the application running
    await app.RunAsync();
}
catch (Exception ex)
{
    // Log the exception and handle any cleanup if necessary
    Log.Fatal(ex, "Unhandled exception in NotificationService");
}
finally
{
    Log.CloseAndFlush();
}

public class NotificationDbContext : DbContext
{
    public DbSet<Notification> Notifications { get; set; }

    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
}

public class NotificationConsumer : IConsumer<Notification>
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationConsumer> _logger;

    public NotificationConsumer(NotificationDbContext context, ILogger<NotificationConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Notification> context)
    {
        var notification = context.Message;

        _logger.LogInformation("Received notification: {Message}", notification.Message);

        // Save notification to the database
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Mock sending notification (e.g., sending an email or push notification)
        _logger.LogInformation("Notification sent: {Message}", notification.Message);
    }
}

public class OrderConsumer : IConsumer<OrderCreated>, IConsumer<OrderUpdated>
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<Order> _logger;

    public OrderConsumer(NotificationDbContext context, ILogger<Order> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var order = context.Message;
        
        _logger.LogInformation("Received order: {OrderId}", order.Id);
        
        var notification = new Notification
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Message = $"Order {order.Id} has been created"
        };

        _logger.LogInformation("Notification sent for order: {OrderId}", order.Id);

        // Save notification to the database
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Mock sending notification (e.g., sending an email or push notification)
        _logger.LogInformation("Notification sent: {Message}", notification.Message);
    }

    public async Task Consume(ConsumeContext<OrderUpdated> context)
    {
        var order = context.Message;
        
        _logger.LogInformation("Received order: {OrderId}", order.Id);
        
        var notification = new Notification
        {
            CreatedAt = DateTime.UtcNow,
            Id = Guid.NewGuid(),
            Message = $"Order {order.Id} has been updated"
        };

        _logger.LogInformation("Notification sent for order: {OrderId}", order.Id);

        // Save notification to the database
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Mock sending notification (e.g., sending an email or push notification)
        _logger.LogInformation("Notification sent: {Message}", notification.Message);
    }
}

public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();

        // Build configuration
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        optionsBuilder.UseNpgsql(connectionString);

        return new NotificationDbContext(optionsBuilder.Options);
    }
}