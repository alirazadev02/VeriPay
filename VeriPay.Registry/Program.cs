using Microsoft.EntityFrameworkCore;
using VeriPay.Shared.Data;
using VeriPay.Registry.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<VeriPayDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("VeriPay")));

builder.Services.AddScoped<RegistryService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VeriPay Registry", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

app.UseCors();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Registry v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();
