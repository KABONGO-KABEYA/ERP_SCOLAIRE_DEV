using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection(BootstrapOptions.SectionName));
builder.Services.AddSingleton<SchoolRegistry>();
builder.Services.AddSingleton<BootstrapSessionStore>();
builder.Services.AddSingleton<
    SchoolManagement.Application.ParentActivation.BootstrapRelay.IBootstrapRelayOutboundAuth,
    StaticSharedKeyBootstrapRelayOutboundAuth>();
builder.Services.AddSingleton<BootstrapOrchestrator>();
builder.Services.AddHttpClient("school-relay");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "bootstrap" }));

app.Run();

public partial class Program;
