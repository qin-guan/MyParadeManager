using MyParadeManager.WebApi.Entities;

namespace MyParadeManager.WebApi.GoogleSheets.ValueConverter;

public class UserTeamRoleConverter : IValueConverter<UserTeamRole>
{
    public UserTeamRole ConvertFromString(string value)
    {
        return new UserTeamRole(value);
    }

    public string ConvertToString(UserTeamRole value)
    {
        return value.Value;
    }
}