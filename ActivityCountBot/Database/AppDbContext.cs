using Microsoft.EntityFrameworkCore;

namespace ActivityCountBot.Database;

public class AppDbContext : DbContext
{
    public DbSet<MessageEntry> Messages => Set<MessageEntry>();

    public AppDbContext() { }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            const string connStr = "Server=localhost;Port=3306;Database=botdb;User=root;Password=root;CharSet=utf8mb4;";
            optionsBuilder.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasCharSet("utf8mb4");
        var msg = modelBuilder.Entity<MessageEntry>();

        msg.ToTable("messages");

        msg.HasKey(m => m.Id);

        msg.Property(m => m.Id)
           .HasColumnName("id")
           .HasColumnType("bigint")
           .ValueGeneratedOnAdd();

        msg.Property(m => m.ChatId)
           .HasColumnName("chat_id")
           .HasColumnType("bigint")
           .IsRequired();

        msg.Property(m => m.UserId)
           .HasColumnName("user_id")
           .HasColumnType("bigint")
           .IsRequired();

        msg.Property(m => m.Username)
           .HasColumnName("username")
           .HasMaxLength(255)
           .HasColumnType("varchar(255)")
           .IsRequired(false);

        msg.Property(m => m.SentAt)
           .HasColumnName("sent_at")
           .HasColumnType("datetime")
           .IsRequired();

        msg.Property(m => m.CreatedAt)
           .HasColumnName("created_at")
           .HasColumnType("datetime")
           .HasDefaultValueSql("CURRENT_TIMESTAMP")
           .ValueGeneratedOnAdd();

        msg.HasIndex(m => new { m.ChatId, m.UserId, m.SentAt })
           .HasDatabaseName("idx_messages_stats");

        msg.HasIndex(m => m.SentAt)
           .HasDatabaseName("idx_messages_cleanup");
    }
}