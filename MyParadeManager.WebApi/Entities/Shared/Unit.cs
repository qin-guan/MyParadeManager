using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Shared;

[Entity(SheetName = "Units")]
public class Unit
{
    [Key]
    [Column(Name = "ID", ColumnLetter = "A")]
    public Guid Id { get; set; }

    [Column(Name = "Name", ColumnLetter = "B")]
    public string Name { get; set; }

    [Column(Name = "SpreadsheetId", ColumnLetter = "C")]
    public string SpreadsheetId { get; set; }
}