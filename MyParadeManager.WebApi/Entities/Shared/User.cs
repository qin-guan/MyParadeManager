using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Shared;

[Entity(SheetName = "Users")]
public class User
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public long Id { get; set; }

    [Column(Name = "Name", ColumnLetter = "B")]
    public string? Name { get; set; }
    
    [Column(Name = "Unit", ColumnLetter = "C")]
    public string? Unit { get; set; }
}