namespace MyParadeManager.WebApi.Utilities;

public static class RangeUtilities
{
    private const string Columns = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    
    public static string GetRange(string sheetName) => sheetName;

    public static string GetRange(string sheetName, string[] header, int fromRow, int toRow) =>
        GetRange(sheetName, header.Length, fromRow, toRow);

    public static string GetRange(string sheetName, int columns, int fromRow, int toRow) =>
        $"{sheetName}!A{fromRow}:{Columns[columns]}{toRow}";
}