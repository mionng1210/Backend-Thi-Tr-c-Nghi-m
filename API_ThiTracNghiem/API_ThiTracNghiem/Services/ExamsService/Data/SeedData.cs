using Microsoft.EntityFrameworkCore;
using ExamsService.Models;

namespace ExamsService.Data
{
    public static class SeedData
    {
        public static async Task SeedAsync(ExamsDbContext context)
        {
            // Seed Subjects first
            if (!await context.Subjects.AnyAsync())
            {
                var subjects = new List<Subject>
                {
                    new Subject
                    {
                        Name = "Lập trình C#",
                        Description = "Ngôn ngữ lập trình C# và .NET Framework",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Subject
                    {
                        Name = "Cơ sở dữ liệu",
                        Description = "SQL Server và thiết kế cơ sở dữ liệu",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Subject
                    {
                        Name = "Web Development",
                        Description = "ASP.NET Core và Web API",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.Subjects.AddRange(subjects);
                await context.SaveChangesAsync();
            }

            // Seed Roles
            if (!await context.Roles.AnyAsync())
            {
                var roles = new List<Role>
                {
                    new Role { Name = "Student", Description = "Học viên", CreatedAt = DateTime.UtcNow },
                    new Role { Name = "Teacher", Description = "Giảng viên", CreatedAt = DateTime.UtcNow },
                    new Role { Name = "Admin", Description = "Quản trị viên", CreatedAt = DateTime.UtcNow }
                };

                context.Roles.AddRange(roles);
                await context.SaveChangesAsync();
            }

            // Seed Users (Teachers)
            if (!await context.Users.AnyAsync())
            {
                var teacherRoleId = await context.Roles.Where(r => r.Name == "Teacher").Select(r => r.RoleId).FirstAsync();

                var users = new List<User>
                {
                    new User
                    {
                        Email = "teacher1@example.com",
                        PasswordHash = "hashed_password_1",
                        FullName = "Nguyễn Văn A",
                        RoleId = teacherRoleId,
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    },
                    new User
                    {
                        Email = "teacher2@example.com",
                        PasswordHash = "hashed_password_2",
                        FullName = "Trần Thị B",
                        RoleId = teacherRoleId,
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.Users.AddRange(users);
                await context.SaveChangesAsync();
            }

            // Seed Courses
            if (!await context.Courses.AnyAsync())
            {
                var subjectIds = await context.Subjects.OrderBy(s => s.SubjectId).Select(s => s.SubjectId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.Role!.Name == "Teacher").OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var courses = new List<Course>
                {
                    new Course
                    {
                        Title = "Lập trình C# cơ bản",
                        Description = "Khóa học C# từ cơ bản đến nâng cao",
                        TeacherId = teacherIds.FirstOrDefault(),
                        SubjectId = subjectIds.FirstOrDefault(),
                        Price = 299000,
                        IsFree = false,
                        Level = "Beginner",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Course
                    {
                        Title = "SQL Server Database",
                        Description = "Thiết kế và quản lý cơ sở dữ liệu SQL Server",
                        TeacherId = teacherIds.Skip(1).FirstOrDefault(),
                        SubjectId = subjectIds.Skip(1).FirstOrDefault(),
                        Price = 399000,
                        IsFree = false,
                        Level = "Intermediate",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Course
                    {
                        Title = "ASP.NET Core Web API",
                        Description = "Xây dựng RESTful API với ASP.NET Core",
                        TeacherId = teacherIds.FirstOrDefault(),
                        SubjectId = subjectIds.Skip(2).FirstOrDefault(),
                        Price = 499000,
                        IsFree = false,
                        Level = "Advanced",
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.Courses.AddRange(courses);
                await context.SaveChangesAsync();
            }

            // Seed Question Banks
            if (!await context.QuestionBanks.AnyAsync())
            {
                var subjectIds = await context.Subjects.OrderBy(s => s.SubjectId).Select(s => s.SubjectId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.Role!.Name == "Teacher").OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var questionBanks = new List<QuestionBank>
                {
                    new QuestionBank
                    {
                        Name = "Ngân hàng câu hỏi C#",
                        Description = "Câu hỏi về lập trình C#",
                        SubjectId = subjectIds.FirstOrDefault(),
                        CreatedBy = teacherIds.FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    },
                    new QuestionBank
                    {
                        Name = "Ngân hàng câu hỏi SQL",
                        Description = "Câu hỏi về SQL Server",
                        SubjectId = subjectIds.Skip(1).FirstOrDefault(),
                        CreatedBy = teacherIds.Skip(1).FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.QuestionBanks.AddRange(questionBanks);
                await context.SaveChangesAsync();
            }

            // Seed Questions
            if (!await context.Questions.AnyAsync())
            {
                var bankIds = await context.QuestionBanks.OrderBy(qb => qb.BankId).Select(qb => qb.BankId).ToListAsync();
                var teacherIds = await context.Users.Where(u => u.Role!.Name == "Teacher").OrderBy(u => u.UserId).Select(u => u.UserId).ToListAsync();

                var questions = new List<Question>
                {
                    new Question
                    {
                        Content = "C# là ngôn ngữ lập trình gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Easy",
                        BankId = bankIds.FirstOrDefault(),
                        CreatedBy = teacherIds.FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Question
                    {
                        Content = "Sự khác biệt giữa class và struct trong C#?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Medium",
                        BankId = bankIds.FirstOrDefault(),
                        CreatedBy = teacherIds.FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Question
                    {
                        Content = "SQL Server là gì?",
                        QuestionType = "MultipleChoice",
                        Difficulty = "Easy",
                        BankId = bankIds.Skip(1).FirstOrDefault(),
                        CreatedBy = teacherIds.Skip(1).FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Question
                    {
                        Content = "Cách tối ưu hóa query trong SQL Server?",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        BankId = bankIds.Skip(1).FirstOrDefault(),
                        CreatedBy = teacherIds.Skip(1).FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    },
                    new Question
                    {
                        Content = "Async/await trong C# hoạt động như thế nào?",
                        QuestionType = "Essay",
                        Difficulty = "Hard",
                        BankId = bankIds.FirstOrDefault(),
                        CreatedBy = teacherIds.FirstOrDefault(),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                context.Questions.AddRange(questions);
                await context.SaveChangesAsync();
            }

            // Seed Answer Options for Multiple Choice Questions
            if (!await context.AnswerOptions.AnyAsync())
            {
                var mcQuestions = await context.Questions
                    .Where(q => q.QuestionType == "MultipleChoice")
                    .OrderBy(q => q.QuestionId)
                    .ToListAsync();

                var answerOptions = new List<AnswerOption>();

                // Options for first question (C# là ngôn ngữ lập trình gì?)
                if (mcQuestions.Count > 0)
                {
                    var q1 = mcQuestions[0];
                    answerOptions.AddRange(new[]
                    {
                        new AnswerOption { QuestionId = q1.QuestionId, Content = "Ngôn ngữ lập trình hướng đối tượng", IsCorrect = true, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q1.QuestionId, Content = "Ngôn ngữ lập trình thủ tục", IsCorrect = false, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q1.QuestionId, Content = "Ngôn ngữ markup", IsCorrect = false, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q1.QuestionId, Content = "Ngôn ngữ script", IsCorrect = false, CreatedAt = DateTime.UtcNow }
                    });
                }

                // Options for second question (class vs struct)
                if (mcQuestions.Count > 1)
                {
                    var q2 = mcQuestions[1];
                    answerOptions.AddRange(new[]
                    {
                        new AnswerOption { QuestionId = q2.QuestionId, Content = "Class là reference type, struct là value type", IsCorrect = true, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q2.QuestionId, Content = "Không có sự khác biệt", IsCorrect = false, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q2.QuestionId, Content = "Class nhỏ hơn struct", IsCorrect = false, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q2.QuestionId, Content = "Struct hỗ trợ inheritance", IsCorrect = false, CreatedAt = DateTime.UtcNow }
                    });
                }

                // Options for third question (SQL Server là gì?)
                if (mcQuestions.Count > 2)
                {
                    var q3 = mcQuestions[2];
                    answerOptions.AddRange(new[]
                    {
                        new AnswerOption { QuestionId = q3.QuestionId, Content = "Hệ quản trị cơ sở dữ liệu quan hệ", IsCorrect = true, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q3.QuestionId, Content = "Ngôn ngữ lập trình", IsCorrect = false, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q3.QuestionId, Content = "Hệ điều hành", IsCorrect = false, CreatedAt = DateTime.UtcNow },
                        new AnswerOption { QuestionId = q3.QuestionId, Content = "Web browser", IsCorrect = false, CreatedAt = DateTime.UtcNow }
                    });
                }

                context.AnswerOptions.AddRange(answerOptions);
                await context.SaveChangesAsync();
            }
        }
    }
}