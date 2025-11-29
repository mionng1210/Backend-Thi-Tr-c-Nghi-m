using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Load Ocelot configuration
var ocelotPath = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
if (File.Exists(ocelotPath))
{
    var jsonText = File.ReadAllText(ocelotPath);
    int depth = 0, endIndex = -1;
    for (int i = 0; i < jsonText.Length; i++)
    {
        var ch = jsonText[i];
        if (ch == '{') depth++;
        else if (ch == '}')
        {
            depth--;
            if (depth == 0) { endIndex = i; break; }
        }
    }
    var cleanJson = endIndex >= 0 ? jsonText.Substring(0, endIndex + 1) : jsonText;
    using var ocelotStream = new MemoryStream(Encoding.UTF8.GetBytes(cleanJson));
    builder.Configuration.AddJsonStream(ocelotStream);
}
else
{
    builder.Configuration
        .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
}
builder.Configuration.AddEnvironmentVariables();

// Add CORS to allow frontend dev servers
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:4000", "http://localhost:4001", "http://localhost:5173", "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Ocelot services
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// Handle CORS preflight (OPTIONS) explicitly before Ocelot
// Ocelot sometimes short-circuits OPTIONS without adding CORS headers
app.Use(async (context, next) =>
{
    if (string.Equals(context.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
    {
        var origin = context.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Vary"] = "Origin";
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            var requestedHeaders = context.Request.Headers["Access-Control-Request-Headers"].ToString();
            if (!string.IsNullOrWhiteSpace(requestedHeaders))
            {
                context.Response.Headers["Access-Control-Allow-Headers"] = requestedHeaders;
            }
            else
            {
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With, X-Gemini-Api-Key";
            }
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.Response.Headers["Access-Control-Max-Age"] = "86400"; // 24h cache for preflight
        }
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }
    await next();
});

app.UseCors("AllowFrontend");

// Ocelot middleware must be awaited
await app.UseOcelot();

app.Run();
