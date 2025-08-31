using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Tenant;

[Entity(SheetName = "SubUnitParadeStateReports")]
public class SubUnitParadeStateReport
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public long Id { get; set; }

    [Column(Name = "SubmittedAt", ColumnLetter = "B")]
    public DateTime SubmittedAt { get; set; }

    [Column(Name = "SubmittedBy", ColumnLetter = "B")]
    public string SubmittedBy { get; set; }

    [Column(Name = "Records", ColumnLetter = "B")]
    public SubUnitParadeStateRecord[] Records { get; set; }
}

public record SubUnitParadeStateRecord(string UserId, bool Present, string Description);