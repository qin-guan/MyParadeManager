namespace MyParadeManager.WebApi.Entities;

public record UserTeamRole
{
    public UserTeamRole(string value)
    {
        if (value is not "Member" and not "Owner")
        {
            throw new Exception("UserTeamRole is not valid.");
        }
        
        Value = value;
    }

    public string Value { get; init; }
}