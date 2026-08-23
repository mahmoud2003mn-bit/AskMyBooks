namespace AskMyBooks.Domain;

public sealed class BookEntry
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string Author { get; private set; } = null!;
    public string Genre { get; private set; } = null!;
    public ReadingStatus Status { get; private set; }
    public int? Rating { get; private set; }          // 1..5
    public string Notes { get; private set; } = "";
    public DateOnly? FinishedOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private BookEntry() { } // لـ EF فقط

    public static BookEntry Create(
        string title,
        string author,
        string genre,
        ReadingStatus status,
        string? notes,
        int? rating,
        DateOnly? finishedOn)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
            throw new ArgumentException("Title is required and must be ≤ 200 characters.", nameof(title));

        if (string.IsNullOrWhiteSpace(author) || author.Length > 200)
            throw new ArgumentException("Author is required and must be ≤ 200 characters.", nameof(author));

        if (string.IsNullOrWhiteSpace(genre))
            throw new ArgumentException("Genre is required.", nameof(genre));

        notes ??= "";
        if (notes.Length > 4000)
            throw new ArgumentException("Notes must be ≤ 4000 characters.", nameof(notes));

        if (status == ReadingStatus.Finished)
        {
            if (rating is null || rating < 1 || rating > 5)
                throw new ArgumentException("Rating (1-5) is required when status is Finished.", nameof(rating));
            if (finishedOn is null)
                throw new ArgumentException("FinishedOn is required when status is Finished.", nameof(finishedOn));
        }
        else
        {
            if (rating is not null)
                throw new ArgumentException("Rating is only allowed when status is Finished.", nameof(rating));
            if (finishedOn is not null)
                throw new ArgumentException("FinishedOn is only allowed when status is Finished.", nameof(finishedOn));
        }

        return new BookEntry
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Author = author.Trim(),
            Genre = genre.Trim(),
            Status = status,
            Rating = rating,
            Notes = notes.Trim(),
            FinishedOn = finishedOn,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkFinished(int rating, DateOnly on)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        Status = ReadingStatus.Finished;
        Rating = rating;
        FinishedOn = on;
    }

    public void UpdateNotes(string notes)
    {
        if (notes.Length > 4000)
            throw new ArgumentException("Notes must be ≤ 4000 characters.", nameof(notes));

        Notes = notes.Trim();
    }

    public string ToEmbeddingText() =>
        $"{Title} by {Author}. Genre: {Genre}. Status: {Status}. " +
        $"Rating: {(Rating is null ? "n/a" : Rating + "/5")}. Notes: {Notes}";
}