using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EventGoNoGoAdvisor.Api.Clients;
using EventGoNoGoAdvisor.Api.Endpoints;
using EventGoNoGoAdvisor.Api.Options;
using EventGoNoGoAdvisor.Api.Rules;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<EventRuleThresholds>(builder.Configuration.GetSection("EventRuleThresholds"));

var upstreamTimeout = TimeSpan.FromSeconds(10);

builder.Services.AddHttpClient<IWeatherClient, OpenMeteoWeatherClient>(client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com");
    client.Timeout = upstreamTimeout;
});

builder.Services.AddHttpClient<IHolidayClient, NagerDateHolidayClient>(client =>
{
    client.BaseAddress = new Uri("https://date.nager.at");
    client.Timeout = upstreamTimeout;
});

builder.Services.AddSingleton<IEventRule, RainProbabilityRule>();
builder.Services.AddSingleton<IEventRule, WindSpeedRule>();
builder.Services.AddSingleton<IEventRule, PublicHolidayRule>();
builder.Services.AddSingleton<EventRuleEngine>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string EventAdvisorRateLimitPolicy = "event-advisor";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(EventAdvisorRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(exceptionHandlerApp => exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "An unexpected error occurred.",
            status = StatusCodes.Status500InternalServerError
        });
    }));
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'";
    await next();
});

app.UseRateLimiter();

app.MapEventAdvisorEndpoints(EventAdvisorRateLimitPolicy);

app.Run();
