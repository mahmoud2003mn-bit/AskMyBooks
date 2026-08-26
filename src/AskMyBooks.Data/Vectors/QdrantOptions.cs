namespace AskMyBooks.Data.Vectors;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public string Collection { get; set; } = "book_entries";
    public int VectorSize { get; set; } = 768;
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.45;
}