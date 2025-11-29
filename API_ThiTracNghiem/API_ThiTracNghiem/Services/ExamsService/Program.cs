using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ExamsService.Data;
using System.Text;
using API_ThiTracNghiem.Services;
using API_ThiTracNghiem.Middleware;
using StackExchange.Redis;
using ExamsService.Services;
using System.Reflection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ExamsService", Version = "v1" });
    
    // Use full type name (including namespace) as schemaId to avoid conflicts
    c.CustomSchemaIds(type => type.FullName);
    
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

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:4000",
            "http://localhost:4100",
            "http://localhost:5173",
            "http://localhost:5505"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

// Add Database Context
builder.Services.AddDbContext<ExamsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
    )
);

// Add Redis connection & exam progress cache
var redisConn = builder.Configuration.GetSection("Redis").GetValue<string>("ConnectionString") ?? "localhost:6379";
// Không để ứng dụng crash nếu Redis chưa sẵn sàng: AbortOnConnectFail=false
var redisOptions = ConfigurationOptions.Parse(redisConn);
redisOptions.AbortOnConnectFail = false;            // tiếp tục retry kết nối nền
redisOptions.ConnectRetry = Math.Max(redisOptions.ConnectRetry, 3);
redisOptions.ConnectTimeout = Math.Max(redisOptions.ConnectTimeout, 5000);
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton<IExamProgressCache, ExamProgressCache>();
builder.Services.AddHostedService<AutoSubmitHostedService>();
builder.Services.AddSingleton<PayOSClient>();

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? ""))
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
app.UseRouting();
app.UseAuthentication();

// User Sync Middleware - Đặt sau Authentication
app.UseUserSync();

app.UseAuthorization();

app.MapControllers();

// Ensure database is migrated and seeded
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ExamsDbContext>();
    
    // Thêm cột Content vào bảng Lessons nếu chưa có (tạm thời fix)
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            IF NOT EXISTS (
                SELECT 1 
                FROM sys.columns 
                WHERE object_id = OBJECT_ID(N'[dbo].[Lessons]') 
                AND name = 'Content'
            )
            BEGIN
                ALTER TABLE [dbo].[Lessons]
                ADD [Content] nvarchar(MAX) NULL;
            END";
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
    }
    catch (Exception ex)
    {
        // Log error nhưng không dừng ứng dụng
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Không thể thêm cột Content vào bảng Lessons. Có thể cột đã tồn tại.");
    }
    
    // Tạo bảng LessonQuestions nếu chưa tồn tại
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            IF NOT EXISTS (
                SELECT 1 
                FROM sys.tables 
                WHERE name = 'LessonQuestions' 
                AND schema_id = SCHEMA_ID('dbo')
            )
            BEGIN
                CREATE TABLE [dbo].[LessonQuestions] (
                    [LessonQuestionId] int IDENTITY(1,1) NOT NULL,
                    [LessonId] int NOT NULL,
                    [QuestionId] int NOT NULL,
                    [SequenceIndex] int NULL,
                    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                    [HasDelete] bit NOT NULL DEFAULT 0,
                    CONSTRAINT [PK_LessonQuestions] PRIMARY KEY ([LessonQuestionId]),
                    CONSTRAINT [FK_LessonQuestions_Lessons_LessonId] FOREIGN KEY ([LessonId]) 
                        REFERENCES [dbo].[Lessons] ([LessonId]) ON DELETE CASCADE,
                    CONSTRAINT [FK_LessonQuestions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) 
                        REFERENCES [dbo].[Questions] ([QuestionId]) ON DELETE CASCADE
                );
                
                CREATE INDEX [IX_LessonQuestions_LessonId] ON [dbo].[LessonQuestions] ([LessonId]);
                CREATE INDEX [IX_LessonQuestions_QuestionId] ON [dbo].[LessonQuestions] ([QuestionId]);
            END";
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("✅ Đã kiểm tra và tạo bảng LessonQuestions nếu cần.");
    }
    catch (Exception ex)
    {
        // Log error nhưng không dừng ứng dụng
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Không thể tạo bảng LessonQuestions. Có thể bảng đã tồn tại hoặc có lỗi.");
    }
    
    // Tạo bảng Feedbacks nếu chưa tồn tại
    try
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            IF NOT EXISTS (
                SELECT 1 
                FROM sys.tables 
                WHERE name = 'Feedbacks' 
                AND schema_id = SCHEMA_ID('dbo')
            )
            BEGIN
                CREATE TABLE [dbo].[Feedbacks] (
                    [FeedbackId] int IDENTITY(1,1) NOT NULL,
                    [UserId] int NULL,
                    [CourseId] int NULL,
                    [ExamId] int NULL,
                    [Rating] int NULL,
                    [Comment] nvarchar(1000) NULL,
                    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt] datetime2 NULL,
                    [HasDelete] bit NOT NULL DEFAULT 0,
                    CONSTRAINT [PK_Feedbacks] PRIMARY KEY ([FeedbackId]),
                    CONSTRAINT [FK_Feedbacks_Users_UserId] FOREIGN KEY ([UserId]) 
                        REFERENCES [dbo].[Users] ([UserId]) ON DELETE SET NULL,
                    CONSTRAINT [FK_Feedbacks_Courses_CourseId] FOREIGN KEY ([CourseId]) 
                        REFERENCES [dbo].[Courses] ([CourseId]) ON DELETE SET NULL,
                    CONSTRAINT [FK_Feedbacks_Exams_ExamId] FOREIGN KEY ([ExamId]) 
                        REFERENCES [dbo].[Exams] ([ExamId]) ON DELETE SET NULL
                );
                
                CREATE INDEX [IX_Feedbacks_UserId] ON [dbo].[Feedbacks] ([UserId]);
                CREATE INDEX [IX_Feedbacks_CourseId] ON [dbo].[Feedbacks] ([CourseId]);
                CREATE INDEX [IX_Feedbacks_ExamId] ON [dbo].[Feedbacks] ([ExamId]);
            END";
        await command.ExecuteNonQueryAsync();
        await connection.CloseAsync();
        
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("✅ Đã kiểm tra và tạo bảng Feedbacks nếu cần.");
    }
    catch (Exception ex)
    {
        // Log error nhưng không dừng ứng dụng
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Không thể tạo bảng Feedbacks. Có thể bảng đã tồn tại hoặc có lỗi.");
    }
    
    dbContext.Database.Migrate();
    
    // Seed data
    await ExamsService.Data.SeedData.SeedAsync(dbContext);
}

app.Run();


