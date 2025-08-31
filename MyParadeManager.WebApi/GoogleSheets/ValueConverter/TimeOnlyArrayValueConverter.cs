using System.Text.Json;
using MyParadeManager.WebApi.Entities.Tenant;

namespace MyParadeManager.WebApi.GoogleSheets.ValueConverter;

public class TimeOnlyArrayValueConverter : IValueConverter<TimeOnly[]>
{
    public TimeOnly[] ConvertFromString(string value)
    {
        return JsonSerializer.Deserialize<TimeOnly[]>(value) ?? throw new Exception($"Failed to deserialize {value}");
    }

    public string ConvertToString(TimeOnly[] value)
    {
        return JsonSerializer.Serialize(value);
    }
}