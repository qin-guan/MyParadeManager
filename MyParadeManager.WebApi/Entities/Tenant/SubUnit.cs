using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Tenant;

[Entity(SheetName = "SubUnits")]
public class SubUnit
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public Guid Id { get; set; }

    [Column(Name = "Name", ColumnLetter = "B")]
    public string Name { get; set; }

    [Column(Name = "InviteCode", ColumnLetter = "C")]
    public string InviteCode { get; set; }
}