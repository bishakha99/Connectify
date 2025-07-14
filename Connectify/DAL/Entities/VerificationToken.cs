using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Connectify.DAL.Entities;

[Table("verification_tokens")]
public partial class VerificationToken
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("otp_code")]
    [StringLength(10)] 
    public string OtpCode { get; set; } = null!;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("VerificationTokens")]
    public virtual User User { get; set; } = null!;
}
