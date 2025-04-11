using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using DatabaseAPI.Utilities;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Initialize the Logger (This is where Logger.Initialize should be called)
Logger.Initialize(builder.Configuration);

// Configure services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// Setup Swagger (optional for testing API endpoints)
builder.Services.AddSwaggerGen(c =>
{
    c.SchemaFilter<DateOnlySchemaFilter>();
});

// Configure CORS to allow cross-origin requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials()); // Allow credentials to be passed along with the request
});

// Configure session management (important for keeping user logged in across requests)
builder.Services.AddDistributedMemoryCache(); // Use in-memory cache for session state
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout in minutes
    options.Cookie.HttpOnly = true; // Prevent JavaScript from accessing session cookies
    options.Cookie.IsEssential = true; // Mark cookie as essential for the app to function
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Make sure to add session middleware here
app.UseCors("AllowAll"); // Enable CORS
app.UseSession(); // Enable session middleware
app.UseRouting();
app.UseAuthorization();

// Map the controllers to the application
app.MapControllers();

app.Run();
