using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities;

[Entity(SheetName = "Teams")]
public class Team
{
    [Key]
    [Column(Name = "ID", ColumnLetter = "A")]
    public int Id { get; set; }

    [Column(Name = "Name", ColumnLetter = "B")]
    public string? Name { get; set; }

    [Column(Name = "InviteCode", ColumnLetter = "C")]
    public string? InviteCode { get; set; }

    [Column(Name = "SpreadsheetId", ColumnLetter = "D")]
    public string? SpreadsheetId { get; set; }

    [Column(Name = "OwnerId", ColumnLetter = "AAA")]
    public long? OwnerId { get; set; }
}