using System.Text.Json;
using MyParadeManager.WebApi.Entities.Tenant;

namespace MyParadeManager.WebApi.GoogleSheets.ValueConverter;

public class SubUnitParadeStateRecordValueConverter : IValueConverter<SubUnitParadeStateRecord>
{
    public SubUnitParadeStateRecord ConvertFromString(string value)
    {
        return JsonSerializer.Deserialize<SubUnitParadeStateRecord>(value) ?? throw new Exception($"Failed to deserialize {value}");
    }

    public string ConvertToString(SubUnitParadeStateRecord value)
    {
        return JsonSerializer.Serialize(value);
    }
}