namespace MyParadeManager.WebApi;

public sealed class BatchUpdateRequest
{
    public required string Range { get; init; }
    public required IList<IList<object>> Values { get; init; }
}