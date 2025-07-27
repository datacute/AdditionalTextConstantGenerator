namespace Datacute.AdditionalTextConstantGenerator;

public readonly record struct AdditionalTextContent(string Path, string? DocCommentCode, string TextContent)
{
    public string Path { get; } = Path;
    public string? DocCommentCode { get; } = DocCommentCode;
    public string TextContent { get; } = TextContent;
}