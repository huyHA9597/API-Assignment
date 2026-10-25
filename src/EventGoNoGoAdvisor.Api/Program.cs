using System.Text.Json.Serialization;
using EventGoNoGoAdvisor.Api.Clients;
using EventGoNoGoAdvisor.Api.Endpoints;
using EventGoNoGoAdvisor.Api.Options;
using EventGoNoGoAdvisor.Api.Rules;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<EventRuleThresholds>(builder.Configuration.GetSection("EventRuleThresholds"));

builder.Services.AddHttpClient<IWeatherClient, OpenMeteoWeatherClient>(client =>
    client.BaseAddress = new Uri("https://api.open-meteo.com"));

builder.Services.AddHttpClient<IHolidayClient, NagerDateHolidayClient>(client =>
    client.BaseAddress = new Uri("https://date.nager.at"));

builder.Services.AddSingleton<IEventRule, RainProbabilityRule>();
builder.Services.AddSingleton<IEventRule, WindSpeedRule>();
builder.Services.AddSingleton<IEventRule, PublicHolidayRule>();
builder.Services.AddSingleton<EventRuleEngine>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapEventAdvisorEndpoints();

app.Run();
