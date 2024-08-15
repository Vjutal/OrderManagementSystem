using Asp.Versioning;
using Contracts;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderService.Authentication.Services;
using RabbitMQ.Client;
using Serilog;

namespace OrderService;

public static class ConfigureServices
{
    public static void AddServices(this WebApplicationBuilder builder)
    {
        builder.AddSerilog();
        builder.AddCors();
        builder.AddHealthChecks();
        builder.AddSwagger();
        builder.AddDatabase();
        builder.AddApiVersioning();
        // builder.Services.AddValidatorsFromAssembly(typeof(ConfigureServices).Assembly);
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();
        builder.Services.AddProblemDetails();
        builder.AddJwtAuthentication();
        builder.AddMassTransit();
    }

    private static void AddSwagger(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));
            options.InferSecuritySchemes();
        });
    }
    
    private static void AddCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(corsPolicyBuilder =>
            {
                corsPolicyBuilder.WithOrigins("*")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }
    
    private static void AddApiVersioning(this WebApplicationBuilder builder)
    {
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });
    }
    
    private static void AddHealthChecks(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty, 
                name: "postgresql", 
                tags: new[] { "db", "sql", "postgresql" })
            .AddRabbitMQ(builder.Configuration.GetConnectionString("RabbitMQConnection") ?? string.Empty, 
                name: "rabbitmq", 
                tags: new[] { "mq", "rabbitmq" });
    }


    private static void AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration);
        });
    }

    private static void AddDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
        });
    }

    private static void AddJwtAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication().AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = Jwt.SecurityKey(builder.Configuration["Jwt:Key"]!),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
        builder.Services.AddTransient<Jwt>();
    }

    private static void AddMassTransit(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.SetSnakeCaseEndpointNameFormatter();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("RabbitMQConnection"));
        
                cfg.ReceiveEndpoint("order_queue", e =>
                {
                    e.ConfigureConsumeTopology = false;
                    
                    e.Durable = true;
                    e.Exclusive = false;
                    e.AutoDelete = false;
                    
                    e.Bind("order_topic", s =>
                    {
                        s.ExchangeType = ExchangeType.Direct;
                    });
                });
        
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}