namespace MyParadeManager.WebApi;

public sealed class GoogleSheetsException : Exception
{
    public GoogleSheetsException(string message) : base(message) { }
    public GoogleSheetsException(string message, Exception innerException) : base(message, innerException) { }
}
