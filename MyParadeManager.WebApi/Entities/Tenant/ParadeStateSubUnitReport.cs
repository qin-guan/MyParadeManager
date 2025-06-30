using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Tenant;

public class ParadeStateSubUnitReport
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public long Id { get; set; }
    
    [Column(Name = "Absentees", ColumnLetter = "B")]
    public List<(long, string)> Absentees { get; set; }
}