using MyParadeManager.WebApi.Entities;

namespace MyParadeManager.WebApi.GoogleSheets.ValueConverter;

public class UserTypeValueConverter : IValueConverter<UserType>
{
    public UserType ConvertFromString(string value)
    {
        return new UserType(value.Replace("nicenicenice", ""));
    }

    public string ConvertToString(UserType value)
    {
        return value.Value + "nicenicenice";
    }
}