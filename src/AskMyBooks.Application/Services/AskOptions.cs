namespace AskMyBooks.Application.Services;

/// <summary>
/// Retrieval knobs for the Ask pipeline. Bound from the same "Qdrant" config
/// section as QdrantOptions — TopK/MinScore are RAG *behavior*, so they live
/// here in Application, while Host/Port/Collection are Qdrant *wiring*, so they
/// stay in Infrastructure. Two options classes, one config section, no
/// cross-layer reference.
/// </summary>
public sealed class AskOptions
{
    public const string SectionName = "Qdrant";

    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.45;
}