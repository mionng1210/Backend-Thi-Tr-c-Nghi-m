using Microsoft.EntityFrameworkCore;
using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Data
{
    public static class SeedData
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Đảm bảo database đã được tạo
            await context.Database.EnsureCreatedAsync();

            // Seed Users (Giáo viên) trước
            if (!await context.Users.AnyAsync())
            {
                var users = new List<User>
                {
                    new User
                    {
                        Email = "teacher1@example.com",
                        PasswordHash = "hashed_password_1", // Trong thực tế cần hash thật
                        FullName = "Nguyễn Văn A",
                        RoleId = 2, // Teacher role
                        Gender = "Nam",
                        Status = "Active",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-35)
                    },
                    new User
                    {
                        Email = "teacher2@example.com",
                        PasswordHash = "hashed_password_2",
                        FullName = "Trần Thị B",
                        RoleId = 2, // Teacher role
                        Gender = "Nữ",
                        Status = "Active",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new User
                    {
                        Email = "teacher3@example.com",
                        PasswordHash = "hashed_password_3",
                        FullName = "Lê Văn C",
                        RoleId = 2, // Teacher role
                        Gender = "Nam",
                        Status = "Active",
                        IsEmailVerified = true,
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    }
                };

                context.Users.AddRange(users);
                await context.SaveChangesAsync();
            }

            // Seed Subjects (Môn học)
            if (!await context.Subjects.AnyAsync())
            {
                var subjects = new List<Subject>
                {
                    new Subject
                    {
                        Name = "Lập trình C#",
                        Description = "Môn học về ngôn ngữ lập trình C# và .NET Framework",
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new Subject
                    {
                        Name = "Cơ sở dữ liệu SQL Server",
                        Description = "Môn học về thiết kế và quản lý cơ sở dữ liệu SQL Server",
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new Subject
                    {
                        Name = "Thiết kế Web với ASP.NET Core",
                        Description = "Môn học về phát triển ứng dụng web sử dụng ASP.NET Core",
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    },
                    new Subject
                    {
                        Name = "Kiến trúc phần mềm",
                        Description = "Môn học về các mẫu thiết kế và kiến trúc phần mềm",
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    },
                    new Subject
                    {
                        Name = "DevOps và CI/CD",
                        Description = "Môn học về DevOps, Docker, và quy trình CI/CD",
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    }
                };

                context.Subjects.AddRange(subjects);
                await context.SaveChangesAsync();
            }

            // Seed ClassCohorts (Lớp học)
            if (!await context.ClassCohorts.AnyAsync())
            {
                // Lấy Subject IDs và Teacher IDs sau khi đã save
                var subjectIds = await context.Subjects.OrderBy(s => s.SubjectId).Select(s => s.SubjectId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.RoleId == 2).OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var classCohorts = new List<ClassCohort>
                {
                    new ClassCohort
                    {
                        Name = "C# Cơ bản - K2024A",
                        SubjectId = subjectIds[0], // Subject đầu tiên (C#)
                        TeacherId = teacherIds[0], // Teacher đầu tiên
                        Level = "Beginner",
                        StartDate = DateTime.UtcNow.AddDays(-30),
                        EndDate = DateTime.UtcNow.AddDays(60),
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new ClassCohort
                    {
                        Name = "SQL Server - K2024B",
                        SubjectId = subjectIds[1], // Subject thứ hai (SQL Server)
                        TeacherId = teacherIds[1], // Teacher thứ hai
                        Level = "Intermediate",
                        StartDate = DateTime.UtcNow.AddDays(-25),
                        EndDate = DateTime.UtcNow.AddDays(65),
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new ClassCohort
                    {
                        Name = "ASP.NET Core - K2024C",
                        SubjectId = subjectIds[2], // Subject thứ ba (ASP.NET Core)
                        TeacherId = teacherIds[0], // Teacher đầu tiên
                        Level = "Advanced",
                        StartDate = DateTime.UtcNow.AddDays(-20),
                        EndDate = DateTime.UtcNow.AddDays(70),
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    },
                    new ClassCohort
                    {
                        Name = "Kiến trúc phần mềm - K2024D",
                        SubjectId = subjectIds[3], // Subject thứ tư (Kiến trúc phần mềm)
                        TeacherId = teacherIds[2], // Teacher thứ ba
                        Level = "Expert",
                        StartDate = DateTime.UtcNow.AddDays(-15),
                        EndDate = DateTime.UtcNow.AddDays(75),
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    },
                    new ClassCohort
                    {
                        Name = "DevOps - K2024E",
                        SubjectId = subjectIds[4], // Subject thứ năm (DevOps)
                        TeacherId = teacherIds[1], // Teacher thứ hai
                        Level = "Advanced",
                        StartDate = DateTime.UtcNow.AddDays(-10),
                        EndDate = DateTime.UtcNow.AddDays(80),
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    }
                };

                context.ClassCohorts.AddRange(classCohorts);
                await context.SaveChangesAsync();
            }

            // Seed Courses (Khóa học)
            if (!await context.Courses.AnyAsync())
            {
                // Lấy Subject IDs và Teacher IDs sau khi đã save
                var subjectIds = await context.Subjects.OrderBy(s => s.SubjectId).Select(s => s.SubjectId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.RoleId == 2).OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var courses = new List<Course>
                {
                    new Course
                    {
                        Title = "Lập trình C# từ cơ bản đến nâng cao",
                        Description = "Khóa học toàn diện về lập trình C#, từ cú pháp cơ bản đến các tính năng nâng cao như LINQ, async/await, và Entity Framework.",
                        TeacherId = teacherIds[0], // Teacher đầu tiên
                        SubjectId = subjectIds[0], // Subject đầu tiên (C#)
                        Price = 299000,
                        IsFree = false,
                        ThumbnailUrl = "https://example.com/images/csharp-course.jpg",
                        DurationMinutes = 1800, // 30 giờ
                        Level = "Beginner",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new Course
                    {
                        Title = "SQL Server Database Design & Optimization",
                        Description = "Học cách thiết kế cơ sở dữ liệu hiệu quả, viết query tối ưu, và quản lý performance trong SQL Server.",
                        TeacherId = teacherIds[1], // Teacher thứ hai
                        SubjectId = subjectIds[1], // Subject thứ hai (SQL Server)
                        Price = 399000,
                        IsFree = false,
                        ThumbnailUrl = "https://example.com/images/sql-course.jpg",
                        DurationMinutes = 2400, // 40 giờ
                        Level = "Intermediate",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new Course
                    {
                        Title = "Xây dựng Web API với ASP.NET Core",
                        Description = "Phát triển RESTful API mạnh mẽ với ASP.NET Core, bao gồm authentication, authorization, và best practices.",
                        TeacherId = teacherIds[0], // Teacher đầu tiên
                        SubjectId = subjectIds[2], // Subject thứ ba (ASP.NET Core)
                        Price = 499000,
                        IsFree = false,
                        ThumbnailUrl = "https://example.com/images/aspnet-course.jpg",
                        DurationMinutes = 3000, // 50 giờ
                        Level = "Advanced",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    },
                    new Course
                    {
                        Title = "Clean Architecture & Design Patterns",
                        Description = "Áp dụng Clean Architecture và các Design Patterns trong phát triển phần mềm enterprise.",
                        TeacherId = teacherIds[2], // Teacher thứ ba
                        SubjectId = subjectIds[3], // Subject thứ tư (Kiến trúc phần mềm)
                        Price = 599000,
                        IsFree = false,
                        ThumbnailUrl = "https://example.com/images/architecture-course.jpg",
                        DurationMinutes = 3600, // 60 giờ
                        Level = "Expert",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    },
                    new Course
                    {
                        Title = "DevOps với Docker & Azure DevOps",
                        Description = "Học DevOps từ cơ bản, containerization với Docker, và CI/CD pipeline với Azure DevOps.",
                        TeacherId = teacherIds[1], // Teacher thứ hai
                        SubjectId = subjectIds[4], // Subject thứ năm (DevOps)
                        Price = 699000,
                        IsFree = false,
                        ThumbnailUrl = "https://example.com/images/devops-course.jpg",
                        DurationMinutes = 4200, // 70 giờ
                        Level = "Advanced",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    }
                };

                context.Courses.AddRange(courses);
                await context.SaveChangesAsync();
            }

            // Seed Materials (Tài liệu)
            if (!await context.Materials.AnyAsync())
            {
                // Lấy Course IDs sau khi đã save
                var courseIds = await context.Courses.OrderBy(c => c.CourseId).Select(c => c.CourseId).ToListAsync();

                var materials = new List<Material>
                {
                    // Tài liệu cho Course 1 (C#)
                    new Material
                    {
                        CourseId = courseIds[0], // Course đầu tiên (C#)
                        Title = "Giới thiệu về C# và .NET Framework",
                        Description = "Tài liệu giới thiệu tổng quan về ngôn ngữ C# và nền tảng .NET Framework, lịch sử phát triển và các tính năng chính.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/csharp-intro.pdf",
                        IsPaid = false,
                        Price = null,
                        OrderIndex = 1,
                        DurationSeconds = 1800, // 30 phút
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new Material
                    {
                        CourseId = courseIds[0], // Course đầu tiên (C#)
                        Title = "Cú pháp cơ bản và biến trong C#",
                        Description = "Học về cú pháp cơ bản, khai báo biến, kiểu dữ liệu, và các toán tử trong C#.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/csharp-syntax.mp4",
                        IsPaid = true,
                        Price = 50000,
                        OrderIndex = 2,
                        DurationSeconds = 3600, // 60 phút
                        CreatedAt = DateTime.UtcNow.AddDays(-29)
                    },
                    new Material
                    {
                        CourseId = courseIds[0], // Course đầu tiên (C#)
                        Title = "Lập trình hướng đối tượng với C#",
                        Description = "Tài liệu chi tiết về OOP trong C#: Class, Object, Inheritance, Polymorphism, và Encapsulation.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/csharp-oop.pdf",
                        IsPaid = true,
                        Price = 75000,
                        OrderIndex = 3,
                        DurationSeconds = 5400, // 90 phút
                        CreatedAt = DateTime.UtcNow.AddDays(-28)
                    },
                    new Material
                    {
                        CourseId = courseIds[0], // Course đầu tiên (C#)
                        Title = "LINQ và Collection trong C#",
                        Description = "Học cách sử dụng LINQ để truy vấn dữ liệu và làm việc với các collection trong C#.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/csharp-linq.mp4",
                        IsPaid = true,
                        Price = 100000,
                        OrderIndex = 4,
                        DurationSeconds = 7200, // 120 phút
                        CreatedAt = DateTime.UtcNow.AddDays(-27)
                    },
                    new Material
                    {
                        CourseId = courseIds[0], // Course đầu tiên (C#)
                        Title = "Async/Await và Multithreading",
                        Description = "Tài liệu về lập trình bất đồng bộ, async/await pattern, và multithreading trong C#.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/csharp-async.pdf",
                        IsPaid = true,
                        Price = 125000,
                        OrderIndex = 5,
                        DurationSeconds = 9000, // 150 phút
                        CreatedAt = DateTime.UtcNow.AddDays(-26)
                    },

                    // Tài liệu cho Course 2 (SQL Server)
                    new Material
                    {
                        CourseId = courseIds[1], // Course thứ hai (SQL Server)
                        Title = "Giới thiệu SQL Server và Management Studio",
                        Description = "Tài liệu giới thiệu về SQL Server, cách cài đặt và sử dụng SQL Server Management Studio.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/sql-intro.pdf",
                        IsPaid = false,
                        Price = null,
                        OrderIndex = 1,
                        DurationSeconds = 1800,
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new Material
                    {
                        CourseId = courseIds[1], // Course thứ hai (SQL Server)
                        Title = "Thiết kế cơ sở dữ liệu và Normalization",
                        Description = "Học cách thiết kế cơ sở dữ liệu hiệu quả, các dạng chuẩn hóa (1NF, 2NF, 3NF, BCNF).",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/sql-design.mp4",
                        IsPaid = true,
                        Price = 80000,
                        OrderIndex = 2,
                        DurationSeconds = 4500,
                        CreatedAt = DateTime.UtcNow.AddDays(-24)
                    },
                    new Material
                    {
                        CourseId = courseIds[1], // Course thứ hai (SQL Server)
                        Title = "T-SQL Advanced Queries",
                        Description = "Tài liệu về các câu lệnh T-SQL nâng cao: CTE, Window Functions, Pivot, và Dynamic SQL.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/sql-advanced.pdf",
                        IsPaid = true,
                        Price = 120000,
                        OrderIndex = 3,
                        DurationSeconds = 6300,
                        CreatedAt = DateTime.UtcNow.AddDays(-23)
                    },
                    new Material
                    {
                        CourseId = courseIds[1], // Course thứ hai (SQL Server)
                        Title = "Performance Tuning và Indexing",
                        Description = "Học cách tối ưu hiệu suất database, tạo và quản lý index, phân tích execution plan.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/sql-performance.mp4",
                        IsPaid = true,
                        Price = 150000,
                        OrderIndex = 4,
                        DurationSeconds = 8100,
                        CreatedAt = DateTime.UtcNow.AddDays(-22)
                    },
                    new Material
                    {
                        CourseId = courseIds[1], // Course thứ hai (SQL Server)
                        Title = "Backup và Recovery Strategies",
                        Description = "Tài liệu về các chiến lược backup và recovery, disaster recovery planning trong SQL Server.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/sql-backup.pdf",
                        IsPaid = true,
                        Price = 100000,
                        OrderIndex = 5,
                        DurationSeconds = 5400,
                        CreatedAt = DateTime.UtcNow.AddDays(-21)
                    },

                    // Tài liệu cho Course 3 (ASP.NET Core)
                    new Material
                    {
                        CourseId = courseIds[2], // Course thứ ba (ASP.NET Core)
                        Title = "Giới thiệu ASP.NET Core và Web API",
                        Description = "Tài liệu giới thiệu về ASP.NET Core, kiến trúc MVC, và cách tạo Web API đầu tiên.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/aspnet-intro.pdf",
                        IsPaid = false,
                        Price = null,
                        OrderIndex = 1,
                        DurationSeconds = 2400,
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    },
                    new Material
                    {
                        CourseId = courseIds[2], // Course thứ ba (ASP.NET Core)
                        Title = "Dependency Injection và Configuration",
                        Description = "Học về Dependency Injection container, configuration management, và appsettings trong ASP.NET Core.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/aspnet-di.mp4",
                        IsPaid = true,
                        Price = 90000,
                        OrderIndex = 2,
                        DurationSeconds = 4800,
                        CreatedAt = DateTime.UtcNow.AddDays(-19)
                    },
                    new Material
                    {
                        CourseId = courseIds[2], // Course thứ ba (ASP.NET Core)
                        Title = "Entity Framework Core và Database First",
                        Description = "Tài liệu về Entity Framework Core, Code First, Database First, và Migration strategies.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/aspnet-ef.pdf",
                        IsPaid = true,
                        Price = 130000,
                        OrderIndex = 3,
                        DurationSeconds = 7200,
                        CreatedAt = DateTime.UtcNow.AddDays(-18)
                    },
                    new Material
                    {
                        CourseId = courseIds[2], // Course thứ ba (ASP.NET Core)
                        Title = "Authentication và Authorization",
                        Description = "Học về JWT authentication, role-based authorization, và security best practices trong Web API.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/aspnet-auth.mp4",
                        IsPaid = true,
                        Price = 160000,
                        OrderIndex = 4,
                        DurationSeconds = 9000,
                        CreatedAt = DateTime.UtcNow.AddDays(-17)
                    },
                    new Material
                    {
                        CourseId = courseIds[2], // Course thứ ba (ASP.NET Core)
                        Title = "API Documentation với Swagger",
                        Description = "Tài liệu về Swagger/OpenAPI, API documentation, và testing với Swagger UI.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/aspnet-swagger.pdf",
                        IsPaid = true,
                        Price = 70000,
                        OrderIndex = 5,
                        DurationSeconds = 3600,
                        CreatedAt = DateTime.UtcNow.AddDays(-16)
                    },

                    // Tài liệu cho Course 4 (Clean Architecture)
                    new Material
                    {
                        CourseId = courseIds[3], // Course thứ tư (Clean Architecture)
                        Title = "Giới thiệu Clean Architecture",
                        Description = "Tài liệu giới thiệu về Clean Architecture, các layer, và nguyên tắc thiết kế.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/clean-arch-intro.pdf",
                        IsPaid = false,
                        Price = null,
                        OrderIndex = 1,
                        DurationSeconds = 3000,
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    },
                    new Material
                    {
                        CourseId = courseIds[3], // Course thứ tư (Clean Architecture)
                        Title = "SOLID Principles và Design Patterns",
                        Description = "Học về SOLID principles và các design patterns phổ biến: Singleton, Factory, Repository, Unit of Work.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/clean-solid.mp4",
                        IsPaid = true,
                        Price = 180000,
                        OrderIndex = 2,
                        DurationSeconds = 10800,
                        CreatedAt = DateTime.UtcNow.AddDays(-14)
                    },
                    new Material
                    {
                        CourseId = courseIds[3], // Course thứ tư (Clean Architecture)
                        Title = "CQRS và Event Sourcing",
                        Description = "Tài liệu về Command Query Responsibility Segregation và Event Sourcing pattern.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/clean-cqrs.pdf",
                        IsPaid = true,
                        Price = 200000,
                        OrderIndex = 3,
                        DurationSeconds = 12600,
                        CreatedAt = DateTime.UtcNow.AddDays(-13)
                    },
                    new Material
                    {
                        CourseId = courseIds[3], // Course thứ tư (Clean Architecture)
                        Title = "Microservices Architecture",
                        Description = "Học về microservices, service communication, API Gateway, và distributed systems.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/clean-microservices.mp4",
                        IsPaid = true,
                        Price = 250000,
                        OrderIndex = 4,
                        DurationSeconds = 14400,
                        CreatedAt = DateTime.UtcNow.AddDays(-12)
                    },
                    new Material
                    {
                        CourseId = courseIds[3], // Course thứ tư (Clean Architecture)
                        Title = "Testing Strategies và TDD",
                        Description = "Tài liệu về Test-Driven Development, unit testing, integration testing, và mocking.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/clean-testing.pdf",
                        IsPaid = true,
                        Price = 150000,
                        OrderIndex = 5,
                        DurationSeconds = 9000,
                        CreatedAt = DateTime.UtcNow.AddDays(-11)
                    },

                    // Tài liệu cho Course 5 (DevOps)
                    new Material
                    {
                        CourseId = courseIds[4], // Course thứ năm (DevOps)
                        Title = "Giới thiệu DevOps và CI/CD",
                        Description = "Tài liệu giới thiệu về DevOps culture, CI/CD pipeline, và các công cụ phổ biến.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/devops-intro.pdf",
                        IsPaid = false,
                        Price = null,
                        OrderIndex = 1,
                        DurationSeconds = 3600,
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    },
                    new Material
                    {
                        CourseId = courseIds[4], // Course thứ năm (DevOps)
                        Title = "Docker Containerization",
                        Description = "Học về Docker, containerization, Dockerfile, và Docker Compose.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/devops-docker.mp4",
                        IsPaid = true,
                        Price = 140000,
                        OrderIndex = 2,
                        DurationSeconds = 8100,
                        CreatedAt = DateTime.UtcNow.AddDays(-9)
                    },
                    new Material
                    {
                        CourseId = courseIds[4], // Course thứ năm (DevOps)
                        Title = "Azure DevOps và Git Workflow",
                        Description = "Tài liệu về Azure DevOps, Git workflow, branching strategies, và pull request process.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/devops-azure.pdf",
                        IsPaid = true,
                        Price = 120000,
                        OrderIndex = 3,
                        DurationSeconds = 7200,
                        CreatedAt = DateTime.UtcNow.AddDays(-8)
                    },
                    new Material
                    {
                        CourseId = courseIds[4], // Course thứ năm (DevOps)
                        Title = "Infrastructure as Code với Terraform",
                        Description = "Học về Infrastructure as Code, Terraform, và quản lý infrastructure trên cloud.",
                        MediaType = "Video",
                        FileUrl = "https://example.com/materials/devops-terraform.mp4",
                        IsPaid = true,
                        Price = 220000,
                        OrderIndex = 4,
                        DurationSeconds = 12600,
                        CreatedAt = DateTime.UtcNow.AddDays(-7)
                    },
                    new Material
                    {
                        CourseId = courseIds[4], // Course thứ năm (DevOps)
                        Title = "Monitoring và Logging",
                        Description = "Tài liệu về application monitoring, logging strategies, và observability trong production.",
                        MediaType = "PDF",
                        FileUrl = "https://example.com/materials/devops-monitoring.pdf",
                        IsPaid = true,
                        Price = 160000,
                        OrderIndex = 5,
                        DurationSeconds = 9000,
                        CreatedAt = DateTime.UtcNow.AddDays(-6)
                    }
                };

                context.Materials.AddRange(materials);
                await context.SaveChangesAsync();
            }

            // Seed QuestionBanks (Ngân hàng câu hỏi)
            if (!await context.QuestionBanks.AnyAsync())
            {
                var subjectIds = await context.Subjects.OrderBy(s => s.SubjectId).Select(s => s.SubjectId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.RoleId == 2).OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var questionBanks = new List<QuestionBank>
                {
                    new QuestionBank
                    {
                        Name = "Ngân hàng câu hỏi C# Cơ bản",
                        Description = "Tập hợp câu hỏi về ngôn ngữ lập trình C# cơ bản",
                        SubjectId = subjectIds[0], // C#
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new QuestionBank
                    {
                        Name = "Ngân hàng câu hỏi SQL Server",
                        Description = "Tập hợp câu hỏi về cơ sở dữ liệu SQL Server",
                        SubjectId = subjectIds[1], // SQL Server
                        CreatedBy = teacherIds[1],
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new QuestionBank
                    {
                        Name = "Ngân hàng câu hỏi ASP.NET Core",
                        Description = "Tập hợp câu hỏi về ASP.NET Core Web API",
                        SubjectId = subjectIds[2], // ASP.NET Core
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    }
                };

                context.QuestionBanks.AddRange(questionBanks);
                await context.SaveChangesAsync();
            }

            // Seed Questions (Câu hỏi)
            if (!await context.Questions.AnyAsync())
            {
                var bankIds = await context.QuestionBanks.OrderBy(b => b.BankId).Select(b => b.BankId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.RoleId == 2).OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var questions = new List<Question>
                {
                    // Questions for C# Bank
                    new Question
                    {
                        BankId = bankIds[0],
                        Content = "Trong C#, từ khóa nào được sử dụng để khai báo một biến không thể thay đổi giá trị?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Easy",
                        Marks = 1,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-30)
                    },
                    new Question
                    {
                        BankId = bankIds[0],
                        Content = "Sự khác biệt giữa 'string' và 'String' trong C# là gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        Marks = 2,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-29)
                    },
                    new Question
                    {
                        BankId = bankIds[0],
                        Content = "Giải thích khái niệm Garbage Collection trong .NET Framework",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        Marks = 5,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-28)
                    },
                    new Question
                    {
                        BankId = bankIds[0],
                        Content = "Trong C#, interface và abstract class khác nhau như thế nào?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        Marks = 3,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-27)
                    },
                    new Question
                    {
                        BankId = bankIds[0],
                        Content = "LINQ là gì và nó được sử dụng để làm gì trong C#?",
                        QuestionType = "Essay",
                        Difficulty = "Medium",
                        Marks = 4,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-26)
                    },

                    // Questions for SQL Server Bank
                    new Question
                    {
                        BankId = bankIds[1],
                        Content = "Câu lệnh SQL nào được sử dụng để lấy dữ liệu từ bảng?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Easy",
                        Marks = 1,
                        CreatedBy = teacherIds[1],
                        CreatedAt = DateTime.UtcNow.AddDays(-25)
                    },
                    new Question
                    {
                        BankId = bankIds[1],
                        Content = "Sự khác biệt giữa INNER JOIN và LEFT JOIN là gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        Marks = 2,
                        CreatedBy = teacherIds[1],
                        CreatedAt = DateTime.UtcNow.AddDays(-24)
                    },
                    new Question
                    {
                        BankId = bankIds[1],
                        Content = "Giải thích các dạng chuẩn hóa (1NF, 2NF, 3NF) trong thiết kế cơ sở dữ liệu",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        Marks = 5,
                        CreatedBy = teacherIds[1],
                        CreatedAt = DateTime.UtcNow.AddDays(-23)
                    },
                    new Question
                    {
                        BankId = bankIds[1],
                        Content = "Index trong SQL Server hoạt động như thế nào?",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        Marks = 4,
                        CreatedBy = teacherIds[1],
                        CreatedAt = DateTime.UtcNow.AddDays(-22)
                    },
                    new Question
                    {
                        BankId = bankIds[1],
                        Content = "Stored Procedure và Function trong SQL Server khác nhau như thế nào?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        Marks = 3,
                        CreatedBy = teacherIds[1],
                        CreatedAt = DateTime.UtcNow.AddDays(-21)
                    },

                    // Questions for ASP.NET Core Bank
                    new Question
                    {
                        BankId = bankIds[2],
                        Content = "Dependency Injection trong ASP.NET Core là gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        Marks = 2,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-20)
                    },
                    new Question
                    {
                        BankId = bankIds[2],
                        Content = "Sự khác biệt giữa AddScoped, AddTransient và AddSingleton là gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Hard",
                        Marks = 3,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-19)
                    },
                    new Question
                    {
                        BankId = bankIds[2],
                        Content = "Middleware trong ASP.NET Core hoạt động như thế nào?",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        Marks = 5,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-18)
                    },
                    new Question
                    {
                        BankId = bankIds[2],
                        Content = "JWT Authentication trong Web API được implement như thế nào?",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        Marks = 4,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-17)
                    },
                    new Question
                    {
                        BankId = bankIds[2],
                        Content = "Model Binding trong ASP.NET Core MVC là gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        Marks = 2,
                        CreatedBy = teacherIds[0],
                        CreatedAt = DateTime.UtcNow.AddDays(-16)
                    }
                };

                context.Questions.AddRange(questions);
                await context.SaveChangesAsync();
            }

            // Seed AnswerOptions (Các lựa chọn trả lời)
            if (!await context.AnswerOptions.AnyAsync())
            {
                var questionIds = await context.Questions.Where(q => q.QuestionType == "MultipleChoice").OrderBy(q => q.QuestionId).Select(q => q.QuestionId).ToListAsync();

                var answerOptions = new List<AnswerOption>();

                // Options for Question 1 (const keyword)
                if (questionIds.Count > 0)
                {
                    answerOptions.AddRange(new[]
                    {
                        new AnswerOption { QuestionId = questionIds[0], Content = "const", IsCorrect = true, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                        new AnswerOption { QuestionId = questionIds[0], Content = "readonly", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                        new AnswerOption { QuestionId = questionIds[0], Content = "static", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-30) },
                        new AnswerOption { QuestionId = questionIds[0], Content = "final", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-30) }
                    });
                }

                // Options for Question 2 (string vs String)
                if (questionIds.Count > 1)
                {
                    answerOptions.AddRange(new[]
                    {
                        new AnswerOption { QuestionId = questionIds[1], Content = "Không có sự khác biệt, 'string' là alias của 'String'", IsCorrect = true, CreatedAt = DateTime.UtcNow.AddDays(-29) },
                        new AnswerOption { QuestionId = questionIds[1], Content = "'string' là kiểu dữ liệu nguyên thủy, 'String' là class", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-29) },
                        new AnswerOption { QuestionId = questionIds[1], Content = "'string' nhanh hơn 'String'", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-29) },
                        new AnswerOption { QuestionId = questionIds[1], Content = "'String' chỉ dùng trong .NET Framework", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-29) }
                    });
                }

                // Options for Question 4 (interface vs abstract class)
                if (questionIds.Count > 2)
                {
                    answerOptions.AddRange(new[]
                    {
                        new AnswerOption { QuestionId = questionIds[2], Content = "Interface không thể có implementation, abstract class có thể có", IsCorrect = true, CreatedAt = DateTime.UtcNow.AddDays(-27) },
                        new AnswerOption { QuestionId = questionIds[2], Content = "Interface chỉ dành cho public members", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-27) },
                        new AnswerOption { QuestionId = questionIds[2], Content = "Abstract class không thể có constructor", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-27) },
                        new AnswerOption { QuestionId = questionIds[2], Content = "Không có sự khác biệt", IsCorrect = false, CreatedAt = DateTime.UtcNow.AddDays(-27) }
                    });
                }

                context.AnswerOptions.AddRange(answerOptions);
                await context.SaveChangesAsync();
            }

            // Seed Exams (Bài thi)
            if (!await context.Exams.AnyAsync())
            {
                var courseIds = await context.Courses.OrderBy(c => c.CourseId).Select(c => c.CourseId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.RoleId == 2).OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var exams = new List<Exam>
                {
                    new Exam
                    {
                        Title = "Kiểm tra C# cơ bản",
                        Description = "Bài kiểm tra về kiến thức C# cơ bản",
                        CourseId = courseIds.FirstOrDefault(),
                        DurationMinutes = 60,
                        TotalQuestions = 10,
                        TotalMarks = 100,
                        PassingMark = 60,
                        ExamType = "Quiz",
                        StartAt = DateTime.UtcNow.AddDays(1),
                        EndAt = DateTime.UtcNow.AddDays(7),
                        RandomizeQuestions = true,
                        AllowMultipleAttempts = false,
                        Status = "Active",
                        CreatedBy = teacherIds.FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow.AddDays(-5)
                    },
                    new Exam
                    {
                        Title = "Thi cuối kỳ SQL Server",
                        Description = "Bài thi cuối kỳ về SQL Server và thiết kế cơ sở dữ liệu",
                        CourseId = courseIds.Skip(1).FirstOrDefault(),
                        DurationMinutes = 120,
                        TotalQuestions = 20,
                        TotalMarks = 200,
                        PassingMark = 120,
                        ExamType = "Final",
                        StartAt = DateTime.UtcNow.AddDays(3),
                        EndAt = DateTime.UtcNow.AddDays(10),
                        RandomizeQuestions = false,
                        AllowMultipleAttempts = false,
                        Status = "Active",
                        CreatedBy = teacherIds.Skip(1).FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow.AddDays(-3)
                    },
                    new Exam
                    {
                        Title = "Kiểm tra ASP.NET Core",
                        Description = "Bài kiểm tra về ASP.NET Core Web API",
                        CourseId = courseIds.Skip(2).FirstOrDefault(),
                        DurationMinutes = 90,
                        TotalQuestions = 15,
                        TotalMarks = 150,
                        PassingMark = 90,
                        ExamType = "Midterm",
                        StartAt = DateTime.UtcNow.AddDays(2),
                        EndAt = DateTime.UtcNow.AddDays(8),
                        RandomizeQuestions = true,
                        AllowMultipleAttempts = true,
                        Status = "Draft",
                        CreatedBy = teacherIds.Skip(2).FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow.AddDays(-2)
                    }
                };

                context.Exams.AddRange(exams);
                await context.SaveChangesAsync();
            }
        }
    }
}
