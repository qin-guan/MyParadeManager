using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities.Tenant;

public class ParadeStateReport
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public long Id { get; set; }

    [Column(Name = "MarkedBy", ColumnLetter = "B")]
    public long MarkedBy { get; set; }
    
    [Column(Name = "MarkedDateTime", ColumnLetter = "C")]
    public DateTime MarkedDateTime { get; set; }
}