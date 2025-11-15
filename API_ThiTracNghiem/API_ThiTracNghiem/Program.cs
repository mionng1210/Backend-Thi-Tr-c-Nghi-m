using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System;
using System.Linq;
using API_ThiTracNghiem.Data;
using API_ThiTracNghiem.Models;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Repositories;
using API_ThiTracNghiem.Infrastructure;
using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new API_ThiTracNghiem.Infrastructure.InputSanitizationFilter());
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API_ThiTracNghiem",
        Version = "v1"
    });
    // JWT Bearer in Swagger
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập 'Bearer {token}'"
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    var securityRequirement = new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    };
    options.AddSecurityRequirement(securityRequirement);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
            builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:5173",
            builder.Configuration["Cors:FrontendOrigin2"] ?? "http://127.0.0.1:5173"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .WithExposedHeaders("Authorization");
    });
});

// DbContext + SQL Server
builder.Services.AddDbContext<API_ThiTracNghiem.Data.ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication + JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
    // Chấp nhận cả 2 kiểu header: "Bearer <token>" hoặc chỉ "<token>"
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var auth = context.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(auth))
            {
                var token = auth.Trim();
                // Loại bỏ tiền tố Bearer nhiều lần nếu có
                var prefix = "Bearer ";
                while (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    token = token.Substring(prefix.Length).TrimStart();
                }
                // Loại bỏ dấu ngoặc kép nếu client gửi kèm
                token = token.Trim('"');
                context.Token = token;
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }
    };
});

// DI services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<ICloudStorage, CloudinaryService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IDocumentStorage, SupabaseStorage>();
builder.Services.AddScoped<IMaterialsRepository, MaterialsRepository>();
builder.Services.AddScoped<IMaterialsService, MaterialsService>();
builder.Services.AddScoped<IExamsRepository, ExamsRepository>();
builder.Services.AddScoped<IExamsService, ExamsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

// Middleware xử lý exception chuẩn
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Ghi log truy cập trái phép
app.UseRoleAuthorizationLogging();

app.MapControllers();

// Seed dữ liệu mặc định (Roles) khi khởi động
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Áp dụng migration nếu chưa có
    db.Database.Migrate();

    void SeedRoles(ApplicationDbContext context)
    {
        var roleNames = new[] { "Admin", "Teacher", "Student" };
        bool changed = false;
        foreach (var name in roleNames)
        {
            if (!context.Roles.Any(r => r.RoleName == name))
            {
                context.Roles.Add(new Role
                {
                    RoleName = name,
                    CreatedAt = DateTime.UtcNow
                });
                changed = true;
            }
        }
        if (changed)
        {
            context.SaveChanges();
        }
    }

    SeedRoles(db);

    // Seed dữ liệu mẫu cho testing
    await SeedData.SeedAsync(db);
}

app.Run();
