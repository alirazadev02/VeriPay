using Microsoft.EntityFrameworkCore;
using VeriPay.Shared.Data;
using VeriPay.Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VeriPayDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("VeriPay")));

builder.Services.AddScoped<OrchestratorService>();
builder.Services.AddSingleton<IdempotencyStore>();
builder.Services.AddSingleton<IdempotencyFilter>();
builder.Services.AddSingleton<WebhookService>();

builder.Services.AddHttpClient("webhook", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VeriPay Orchestrator", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VeriPayDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orchestrator v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();
