namespace AskMyBooks.Application.Exceptions;

public sealed class LlmContractException : Exception
{
    public string? RawModelOutput { get; }

    public LlmContractException(string message, string? rawModelOutput = null)
        : base(message)
    {
        RawModelOutput = rawModelOutput;
    }
}