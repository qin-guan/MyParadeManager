namespace MyParadeManager.WebApi.GoogleSheets.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class EntityAttribute : Attribute
{
    public string? SheetId { get; set; }
    public string? SheetName { get; set; }
    public bool HasHeader { get; set; } = true;
    public int StartRow { get; set; } = 1;
}