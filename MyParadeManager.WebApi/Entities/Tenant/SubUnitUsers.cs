using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Tenant;

[Entity(SheetName = "SubUnitUsers")]
public class SubUnitUsers
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public Guid Id { get; set; }

    [Column(Name = "UserId", ColumnLetter = "B")]
    public long UserId { get; set; }

    [Column(Name = "SubUnitId", ColumnLetter = "C")]
    public Guid SubUnitId { get; set; }
}