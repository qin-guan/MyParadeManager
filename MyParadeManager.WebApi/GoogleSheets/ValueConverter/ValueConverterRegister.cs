using System.Collections.Concurrent;

namespace MyParadeManager.WebApi.GoogleSheets.ValueConverter;

public static class ValueConverterRegistry
{
    private static readonly ConcurrentDictionary<Type, IValueConverter> Converters = new();

    public static void RegisterConverter<T>(IValueConverter<T> converter)
    {
        Converters[typeof(T)] = new ValueConverterWrapper<T>(converter);
    }

    public static void RegisterConverter(Type type, IValueConverter converter)
    {
        Converters[type] = converter;
    }

    public static IValueConverter? GetConverter(Type type)
    {
        Converters.TryGetValue(type, out var converter);
        return converter;
    }

    public static bool HasConverter(Type type)
    {
        return Converters.ContainsKey(type);
    }

    public static void ClearConverters()
    {
        Converters.Clear();
    }

    private class ValueConverterWrapper<T> : IValueConverter
    {
        private readonly IValueConverter<T> _converter;

        public ValueConverterWrapper(IValueConverter<T> converter)
        {
            _converter = converter;
        }

        public Type TargetType => typeof(T);

        public object? ConvertFromString(string value)
        {
            return _converter.ConvertFromString(value);
        }

        public string ConvertToString(object? value)
        {
            return _converter.ConvertToString((T)value!);
        }
    }
}