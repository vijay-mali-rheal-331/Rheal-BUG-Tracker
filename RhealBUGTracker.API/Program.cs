using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using RhealBUGTracker.API.Middleware;
using RhealBUGTracker.Application.DTOs;
using RhealBUGTracker.Application.Features.Sessions;
using RhealBUGTracker.Application.Validators;
using RhealBUGTracker.Infrastructure;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File("logs/rheal-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName());

    // Infrastructure
    builder.Services.AddInfrastructure(builder.Configuration);

    // MediatR — scan Application assembly
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateSessionCommand).Assembly));

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<CreateSessionRequestValidator>();

    // Rate limiting
    builder.Services.AddRateLimiter(opts =>
    {
        opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                }));
        opts.RejectionStatusCode = 429;
    });

    builder.Services.AddControllers();

    // Swagger
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "RhealBUGTracker API", Version = "v1", Description = "AI-powered code analysis engine" });
        c.AddSecurityDefinition("ApiKey", new()
        {
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Name = "X-Api-Key",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
        });
    });

    // CORS
    builder.Services.AddCors(opts =>
        opts.AddPolicy("AllowFrontend", p =>
            p.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"])
             .AllowAnyHeader()
             .AllowAnyMethod()));

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseCors("AllowFrontend");
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
