using AskMyBooks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AskMyBooks.Data.Persistence.Configurations;

public sealed class BookEntryConfiguration : IEntityTypeConfiguration<BookEntry>
{
    public void Configure(EntityTypeBuilder<BookEntry> builder)
    {
        builder.ToTable("books");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Author)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Genre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.Rating);

        builder.Property(b => b.Notes)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(b => b.FinishedOn);

        builder.Property(b => b.CreatedAt)
            .IsRequired();
    }
}