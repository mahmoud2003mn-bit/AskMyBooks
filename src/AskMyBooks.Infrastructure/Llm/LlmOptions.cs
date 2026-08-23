namespace AskMyBooks.Infrastructure.Llm;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "qwen2.5:7b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}