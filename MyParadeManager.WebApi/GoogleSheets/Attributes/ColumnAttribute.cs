namespace MyParadeManager.WebApi.GoogleSheets.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ColumnAttribute : Attribute
{
    public string? Name { get; set; }
    public int? Order { get; set; }
    public string? ColumnLetter { get; set; }
    public Type? ConverterType { get; set; }
}