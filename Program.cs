using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateBuilder(args);

// Initialize services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS Configuration: Allow any origin (for testing purposes)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecific",
        builder => builder
            .WithOrigins("https://rotc.bpc-bsis4d.com") // Set specific allowed origin
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});


// Add session management if needed
builder.Services.AddDistributedMemoryCache(); // In-memory cache for session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Enable middleware
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll"); // Enable the CORS policy
app.UseSession(); // Enable session support
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.Run();
