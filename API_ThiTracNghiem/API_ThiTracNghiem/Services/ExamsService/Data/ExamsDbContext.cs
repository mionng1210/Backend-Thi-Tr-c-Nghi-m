using Microsoft.EntityFrameworkCore;
using ExamsService.Models;

namespace ExamsService.Data
{
    public class ExamsDbContext : DbContext
    {
        public ExamsDbContext(DbContextOptions<ExamsDbContext> options) : base(options)
        {
        }

        public DbSet<Exam> Exams { get; set; }
        public DbSet<ExamQuestion> ExamQuestions { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionBank> QuestionBanks { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Subject> Subjects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Exam entity
            modelBuilder.Entity<Exam>(entity =>
            {
                entity.HasKey(e => e.ExamId);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.ExamType).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                // Foreign key relationships
                entity.HasOne(e => e.Course)
                    .WithMany()
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.SetNull);
                    
                entity.HasOne(e => e.Creator)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure ExamQuestion entity
            modelBuilder.Entity<ExamQuestion>(entity =>
            {
                entity.HasKey(eq => eq.ExamQuestionId);
                entity.Property(eq => eq.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                entity.HasOne(eq => eq.Exam)
                    .WithMany()
                    .HasForeignKey(eq => eq.ExamId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(eq => eq.Question)
                    .WithMany()
                    .HasForeignKey(eq => eq.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Question entity
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasKey(q => q.QuestionId);
                entity.Property(q => q.Content).IsRequired();
                entity.Property(q => q.QuestionType).HasMaxLength(50);
                entity.Property(q => q.Difficulty).HasMaxLength(50);
                entity.Property(q => q.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                entity.HasOne(q => q.Bank)
                    .WithMany()
                    .HasForeignKey(q => q.BankId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(q => q.Creator)
                    .WithMany()
                    .HasForeignKey(q => q.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure QuestionBank entity
            modelBuilder.Entity<QuestionBank>(entity =>
            {
                entity.HasKey(qb => qb.BankId);
                entity.Property(qb => qb.Name).IsRequired().HasMaxLength(200);
                entity.Property(qb => qb.Description).HasMaxLength(2000);
                entity.Property(qb => qb.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                entity.HasOne(qb => qb.Subject)
                    .WithMany()
                    .HasForeignKey(qb => qb.SubjectId)
                    .OnDelete(DeleteBehavior.SetNull);
                    
                entity.HasOne(qb => qb.Creator)
                    .WithMany()
                    .HasForeignKey(qb => qb.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure AnswerOption entity
            modelBuilder.Entity<AnswerOption>(entity =>
            {
                entity.HasKey(ao => ao.OptionId);
                entity.Property(ao => ao.Content).IsRequired();
                entity.Property(ao => ao.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                entity.HasOne(ao => ao.Question)
                    .WithMany()
                    .HasForeignKey(ao => ao.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Course entity
            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(c => c.CourseId);
                entity.Property(c => c.Title).IsRequired().HasMaxLength(200);
                entity.Property(c => c.Description).HasMaxLength(2000);
                entity.Property(c => c.ThumbnailUrl).HasMaxLength(500);
                entity.Property(c => c.Level).HasMaxLength(50);
                entity.Property(c => c.Status).HasMaxLength(50);
                entity.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                entity.HasOne(c => c.Teacher)
                    .WithMany()
                    .HasForeignKey(c => c.TeacherId)
                    .OnDelete(DeleteBehavior.SetNull);
                    
                entity.HasOne(c => c.Subject)
                    .WithMany()
                    .HasForeignKey(c => c.SubjectId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.Email).HasMaxLength(256);
                entity.Property(u => u.PhoneNumber).HasMaxLength(30);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(u => u.FullName).HasMaxLength(150);
                entity.Property(u => u.Gender).HasMaxLength(20);
                entity.Property(u => u.AvatarUrl).HasMaxLength(500);
                entity.Property(u => u.Status).HasMaxLength(50);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                
                entity.HasOne(u => u.Role)
                    .WithMany()
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure Role entity
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleId);
                entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
                entity.Property(r => r.Description).HasMaxLength(200);
                entity.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Configure Subject entity
            modelBuilder.Entity<Subject>(entity =>
            {
                entity.HasKey(s => s.SubjectId);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(200);
                entity.Property(s => s.Description).HasMaxLength(1000);
                entity.Property(s => s.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });
        }
    }
}