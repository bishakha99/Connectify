using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Connectify.DAL.Entities;

[Table("messages")]
public partial class Message
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("sender_id")]
    public long SenderId { get; set; }

    [Column("receiver_id")]
    public long ReceiverId { get; set; }

    [Column("message_text")]
    public string? MessageText { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [ForeignKey("ReceiverId")]
    [InverseProperty("MessageReceivers")]
    public virtual User Receiver { get; set; } = null!;

    [ForeignKey("SenderId")]
    [InverseProperty("MessageSenders")]
    public virtual User Sender { get; set; } = null!;
}
