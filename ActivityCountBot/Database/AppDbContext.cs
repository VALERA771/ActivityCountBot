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
            const string connStr = "Server=localhost;Port=3306;Database=botdb;User=root;Password=12Rico34;CharSet=utf8mb4;";
            optionsBuilder.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasCharSet("utf8mb4");
        var msg = modelBuilder.Entity<MessageEntry>();

        // Имя таблицы — как в исходном DDL (движок InnoDB и charset utf8mb4_unicode_ci
        // задаются на уровне сервера/БД либо SQL-скриптом, см. README/database schema).
        msg.ToTable("messages");

        msg.HasKey(m => m.Id);

        msg.Property(m => m.Id)
           .HasColumnName("id")
           .HasColumnType("bigint")
           .ValueGeneratedOnAdd();                 // AUTO_INCREMENT

        msg.Property(m => m.ChatId)
           .HasColumnName("chat_id")
           .HasColumnType("bigint")
           .IsRequired();                          // NOT NULL

        msg.Property(m => m.UserId)
           .HasColumnName("user_id")
           .HasColumnType("bigint")
           .IsRequired();                          // NOT NULL

        msg.Property(m => m.Username)
           .HasColumnName("username")
           .HasMaxLength(255)
           .HasColumnType("varchar(255)")
           .IsRequired(false);                     // NULL допустим

        msg.Property(m => m.SentAt)
           .HasColumnName("sent_at")
           .HasColumnType("datetime")
           .IsRequired();                          // NOT NULL

        msg.Property(m => m.CreatedAt)
           .HasColumnName("created_at")
           .HasColumnType("datetime")
           .HasDefaultValueSql("CURRENT_TIMESTAMP") // DEFAULT CURRENT_TIMESTAMP
           .ValueGeneratedOnAdd();                  // EF читает значение после INSERT

        // Индексы из DDL.
        msg.HasIndex(m => new { m.ChatId, m.UserId, m.SentAt })
           .HasDatabaseName("idx_messages_stats");

        msg.HasIndex(m => m.SentAt)
           .HasDatabaseName("idx_messages_cleanup");
    }
}