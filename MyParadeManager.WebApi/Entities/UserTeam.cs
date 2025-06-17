using System.ComponentModel.DataAnnotations;
using MyParadeManager.WebApi.GoogleSheets.Attributes;

namespace MyParadeManager.WebApi.Entities;

[Entity(SheetName = "UserTeams")]
public class UserTeam
{
    [Key]
    [Column(Name = "ID", ColumnLetter = "A")]
    public Guid Id { get; set; }

    [Column(Name = "UserId", ColumnLetter = "B")]
    public long UserId { get; set; }

    [Column(Name = "TeamId", ColumnLetter = "C")]
    public Guid TeamId { get; set; }
    
    [Column(Name = "Role", ColumnLetter = "D")]
    public UserTeamRole Role { get; set; }
}