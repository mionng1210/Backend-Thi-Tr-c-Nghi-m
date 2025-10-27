using Microsoft.EntityFrameworkCore;
using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<AuthSession> AuthSessions { get; set; } = null!;
        public DbSet<OTP> OTPs { get; set; } = null!;
        public DbSet<Subject> Subjects { get; set; } = null!;
        public DbSet<ClassCohort> ClassCohorts { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<Material> Materials { get; set; } = null!;
        public DbSet<Enrollment> Enrollments { get; set; } = null!;
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<QuestionBank> QuestionBanks { get; set; } = null!;
        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<AnswerOption> AnswerOptions { get; set; } = null!;
        public DbSet<Exam> Exams { get; set; } = null!;
        public DbSet<ExamQuestion> ExamQuestions { get; set; } = null!;
        public DbSet<ExamAttempt> ExamAttempts { get; set; } = null!;
        public DbSet<SubmittedAnswer> SubmittedAnswers { get; set; } = null!;
        public DbSet<Result> Results { get; set; } = null!;
        public DbSet<Certificate> Certificates { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;
        public DbSet<Report> Reports { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<NotificationSetting> NotificationSettings { get; set; } = null!;
        public DbSet<ChatThread> ChatThreads { get; set; } = null!;
        public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
        public DbSet<AiRequest> AiRequests { get; set; } = null!;
        public DbSet<Statistics> Statistics { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Certificate: hai quan hệ đến User -> cần chỉ định khóa ngoại rõ ràng
            modelBuilder.Entity<Certificate>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Certificate>()
                .HasOne(c => c.Creator)
                .WithMany()
                .HasForeignKey(c => c.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Report: Reporter và Assignee đều trỏ tới User
            modelBuilder.Entity<Report>()
                .HasOne(r => r.Reporter)
                .WithMany()
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.Assignee)
                .WithMany()
                .HasForeignKey(r => r.AssignedTo)
                .OnDelete(DeleteBehavior.Restrict);

            // QuestionBank: CreatedBy -> User
            modelBuilder.Entity<QuestionBank>()
                .HasOne(qb => qb.Creator)
                .WithMany()
                .HasForeignKey(qb => qb.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Question: CreatedBy -> User
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Creator)
                .WithMany()
                .HasForeignKey(q => q.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Exam: CreatedBy -> User
            modelBuilder.Entity<Exam>()
                .HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // ChatThread: CreatedBy -> User
            modelBuilder.Entity<ChatThread>()
                .HasOne(ct => ct.Creator)
                .WithMany()
                .HasForeignKey(ct => ct.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // ChatMessage: SenderId -> User
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoice: tắt cascade để tránh multiple cascade paths (User -> Invoice và User -> PaymentTransaction -> Invoice)
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Transaction)
                .WithMany()
                .HasForeignKey(i => i.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Result: tắt cascade trên các quan hệ đến Exam/Attempt/User
            modelBuilder.Entity<Result>()
                .HasOne(r => r.Exam)
                .WithMany()
                .HasForeignKey(r => r.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Result>()
                .HasOne(r => r.Attempt)
                .WithMany()
                .HasForeignKey(r => r.AttemptId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Result>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}


