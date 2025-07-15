using System;
using System.Collections.Generic;
using Connectify.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connectify.DAL;

public partial class ConnectifyDb : DbContext
{
    public ConnectifyDb()
    {
    }

    public ConnectifyDb(DbContextOptions<ConnectifyDb> options)
        : base(options)
    {
    }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VerificationToken> VerificationTokens { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseNpgsql("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("messages_pkey");

            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.SentAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.Receiver).WithMany(p => p.MessageReceivers).HasConstraintName("messages_receiver_id_fkey");

            entity.HasOne(d => d.Sender).WithMany(p => p.MessageSenders).HasConstraintName("messages_sender_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.IsActive).HasDefaultValue(false);
            entity.Property(e => e.IsVerified).HasDefaultValue(false);
        });

        modelBuilder.Entity<VerificationToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("verification_tokens_pkey");

            entity.HasOne(d => d.User).WithMany(p => p.VerificationTokens).HasConstraintName("verification_tokens_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
