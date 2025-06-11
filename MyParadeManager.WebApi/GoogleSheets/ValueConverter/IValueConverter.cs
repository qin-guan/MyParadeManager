namespace MyParadeManager.WebApi.GoogleSheets.ValueConverter;

public interface IValueConverter<T>
{
    T ConvertFromString(string value);
    string ConvertToString(T value);
}

public interface IValueConverter
{
    Type TargetType { get; }
    object? ConvertFromString(string value);
    string ConvertToString(object? value);
}