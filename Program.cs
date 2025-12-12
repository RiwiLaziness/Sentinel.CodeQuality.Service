using Sentinel.CodeQuality.Service.Publishers;
using Sentinel.CodeQuality.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IReportPublisher, ReportPublisher>();

// Servicios de dominio
builder.Services.AddScoped<IResultReader, VolumeFileReader>();
builder.Services.AddScoped<SemgrepMapper>();
builder.Services.AddScoped<QualityGateEvaluator>();

// Publicadores
builder.Services.AddScoped<IReportPublisher, ReportPublisher>();

builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();

app.Run();