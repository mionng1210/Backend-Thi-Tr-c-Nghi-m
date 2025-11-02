using Microsoft.EntityFrameworkCore;
using ChatService.Models;

namespace ChatService.Data
{
    public class ChatDbContext : DbContext
    {
        public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
        {
        }

        public DbSet<ChatRoom> ChatRooms { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatRoomMember> ChatRoomMembers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Role first (no dependencies)
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.RoleId);
                entity.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
                entity.Property(r => r.Description).HasMaxLength(200);
                entity.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(r => r.HasDelete).HasDefaultValue(false);
                
                // Unique constraint
                entity.HasIndex(r => r.RoleName).IsUnique();
            });

            // Configure User (depends on Role)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.Email).HasMaxLength(256);
                entity.Property(u => u.PhoneNumber).HasMaxLength(30);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(u => u.FullName).HasMaxLength(150);
                entity.Property(u => u.Gender).HasMaxLength(20);
                entity.Property(u => u.AvatarUrl).HasMaxLength(500);
                entity.Property(u => u.Address).HasMaxLength(500);
                entity.Property(u => u.Status).HasMaxLength(50);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(u => u.HasDelete).HasDefaultValue(false);
                
                // Unique constraints
                entity.HasIndex(u => u.Email).IsUnique();
            });

            // Configure ChatRoom (no dependencies)
            modelBuilder.Entity<ChatRoom>(entity =>
            {
                entity.HasKey(cr => cr.RoomId);
                entity.Property(cr => cr.Name).IsRequired().HasMaxLength(200);
                entity.Property(cr => cr.Description).HasMaxLength(1000);
                entity.Property(cr => cr.RoomType).HasMaxLength(50).HasDefaultValue("general");
                entity.Property(cr => cr.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(cr => cr.IsActive).HasDefaultValue(true);
                entity.Property(cr => cr.HasDelete).HasDefaultValue(false);
            });

            // Configure ChatMessage (depends on ChatRoom and User)
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(cm => cm.MessageId);
                entity.Property(cm => cm.Content).IsRequired();
                entity.Property(cm => cm.MessageType).HasMaxLength(50).HasDefaultValue("text");
                entity.Property(cm => cm.SentAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(cm => cm.IsEdited).HasDefaultValue(false);
                entity.Property(cm => cm.HasDelete).HasDefaultValue(false);
            });

            // Configure ChatRoomMember (depends on ChatRoom and User)
            modelBuilder.Entity<ChatRoomMember>(entity =>
            {
                entity.HasKey(crm => crm.MemberId);
                entity.Property(crm => crm.Role).HasMaxLength(50).HasDefaultValue("member");
                entity.Property(crm => crm.JoinedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(crm => crm.IsActive).HasDefaultValue(true);
                
                // Unique constraint for room-user combination
                entity.HasIndex(crm => new { crm.RoomId, crm.UserId }).IsUnique();
            });
        }
    }
}