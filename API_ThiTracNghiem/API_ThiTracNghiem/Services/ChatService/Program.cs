using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ChatService.Data;
using ChatService.Services;
using ChatService.Middleware;
using ChatService.Hubs;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ChatService", Version = "v1" });
    
    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Enter your JWT token (without 'Bearer' prefix)",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
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
            new List<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:4000", "http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add SignalR
builder.Services.AddSignalR();

// Add Database Context
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

Console.WriteLine($"JWT Key: {jwtKey}");
Console.WriteLine($"JWT Issuer: {jwtIssuer}");
Console.WriteLine($"JWT Audience: {jwtAudience}");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? ""))
        };

        // Configure JWT for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Authentication Failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("JWT Token Validated Successfully");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"JWT Challenge: {context.Error} - {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// User Sync Service
builder.Services.AddHttpClient<IUserSyncService, UserSyncService>();
builder.Services.AddScoped<IUserSyncService, UserSyncService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();

// User Sync Middleware - Đặt sau Authentication
app.UseUserSync();

app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub
app.MapHub<ChatHub>("/chatHub");

// Ensure required tables exist, then migrate
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users](
        [UserId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Email] NVARCHAR(256) NULL,
        [PhoneNumber] NVARCHAR(30) NULL,
        [PasswordHash] NVARCHAR(256) NOT NULL,
        [FullName] NVARCHAR(150) NULL,
        [RoleId] INT NULL,
        [Gender] NVARCHAR(20) NULL,
        [DateOfBirth] DATETIME2 NULL,
        [AvatarUrl] NVARCHAR(500) NULL,
        [Address] NVARCHAR(500) NULL,
        [IsEmailVerified] BIT NOT NULL DEFAULT(0),
        [IsPhoneVerified] BIT NOT NULL DEFAULT(0),
        [Status] NVARCHAR(50) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        [HasDelete] BIT NOT NULL DEFAULT(0)
    );
    CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users]([Email]) WHERE [Email] IS NOT NULL;
END

IF OBJECT_ID(N'[dbo].[Roles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Roles](
        [RoleId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoleName] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(200) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
        [HasDelete] BIT NOT NULL DEFAULT(0)
    );
END

IF OBJECT_ID(N'[dbo].[ChatRooms]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatRooms](
        [RoomId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [RoomType] NVARCHAR(50) NOT NULL,
        [CourseId] INT NULL,
        [ExamId] INT NULL,
        [CreatedBy] INT NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
        [IsActive] BIT NOT NULL DEFAULT(1),
        [HasDelete] BIT NOT NULL DEFAULT(0)
    );
    CREATE INDEX [IX_ChatRooms_CreatedBy] ON [dbo].[ChatRooms]([CreatedBy]);
END

IF OBJECT_ID(N'[dbo].[ChatRoomMembers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatRoomMembers](
        [MemberId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoomId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [Role] NVARCHAR(50) NOT NULL,
        [JoinedAt] DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
        [LastSeenAt] DATETIME2 NULL,
        [IsActive] BIT NOT NULL DEFAULT(1)
    );
    CREATE UNIQUE INDEX [IX_ChatRoomMembers_RoomId_UserId] ON [dbo].[ChatRoomMembers]([RoomId],[UserId]);
    CREATE INDEX [IX_ChatRoomMembers_UserId] ON [dbo].[ChatRoomMembers]([UserId]);
END

IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages](
        [MessageId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [RoomId] INT NOT NULL,
        [SenderId] INT NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [MessageType] NVARCHAR(50) NOT NULL,
        [AttachmentUrl] NVARCHAR(500) NULL,
        [AttachmentName] NVARCHAR(200) NULL,
        [ReplyToMessageId] INT NULL,
        [SentAt] DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME()),
        [IsEdited] BIT NOT NULL DEFAULT(0),
        [EditedAt] DATETIME2 NULL,
        [HasDelete] BIT NOT NULL DEFAULT(0)
    );
    CREATE INDEX [IX_ChatMessages_RoomId] ON [dbo].[ChatMessages]([RoomId]);
    CREATE INDEX [IX_ChatMessages_SenderId] ON [dbo].[ChatMessages]([SenderId]);
END
";
            await cmd.ExecuteNonQueryAsync();
        }

        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogError(ex, "Database initialization failed");
        throw;
    }
}

app.Run();
