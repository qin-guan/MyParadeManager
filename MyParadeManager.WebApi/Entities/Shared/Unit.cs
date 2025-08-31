using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Shared;

[Entity(SheetName = "Units")]
public class Unit
{
    [Key]
    [Column(Name = "Code", ColumnLetter = "A")]
    public string Code { get; set; }

    [Column(Name = "Name", ColumnLetter = "B")]
    public string Name { get; set; }

    [Column(Name = "SpreadsheetId", ColumnLetter = "C")]
    public string SpreadsheetId { get; set; }

    [Column(Name = "ParadeTimings", ColumnLetter = "D")]
    public TimeOnly[] ParadeTimings { get; set; } =
    [
        new(8, 0),
        new(16, 30)
    ];
}