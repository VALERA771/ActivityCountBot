using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ActivityCountBot.Database;

[Table("messages")]
[Index(nameof(ChatId), nameof(UserId), nameof(SentAt), Name = "idx_message_stats")]
[Index(nameof(SentAt), Name = "idx_messages_cleanup")]
public class MessageEntry
{
    [Key]
    [Column("id", TypeName = "bigint")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("chat_id", TypeName = "bigint")]
    public long ChatId { get; set; }

    [Column("user_id", TypeName = "bigint")]
    public long UserId { get; set; }

    [Column("username", TypeName = "varchar(255)")]
    [MaxLength(255)]
    public string? Username { get; set; }
    
    [Column("sent_at", TypeName = "datetime")]
    public DateTime SentAt { get; set; }
    
    [Column("created_at", TypeName = "datetime")]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreatedAt { get; set; }

    public override string ToString()
        => $"#{Id} chat={ChatId} user={UserId} (@{Username ?? "?"}) sent={SentAt:O}";
}