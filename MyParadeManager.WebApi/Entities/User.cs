using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;
using MyParadeManager.WebApi.GoogleSheets.ValueConverter;

namespace MyParadeManager.WebApi.Entities;

[Entity(SheetName = "Users")]
public class User
{
    [Key]
    [Column(Name = "Id", ColumnLetter = "A")]
    public long Id { get; set; }

    [Column(Name = "Name", ColumnLetter = "B")]
    public string? Name { get; set; }

    [Column(Name = "UserType", ColumnLetter = "C", ConverterType = typeof(UserTypeValueConverter))]
    public UserType? UserType { get; set; }
}