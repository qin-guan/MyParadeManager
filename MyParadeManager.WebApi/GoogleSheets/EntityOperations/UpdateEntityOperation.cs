using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace MyParadeManager.WebApi.GoogleSheets.EntityOperations;

public class UpdateEntityOperation<T>(
    T entity,
    string sheetId,
    string sheetName,
    object keyValue,
    string keyPropertyName,
    int keyColumnIndex,
    bool hasHeader,
    int startRow,
    string maxColumnLetter,
    List<object> values
) : IEntityOperation where T : class
{
    private readonly T _entity = entity;

    public async Task ExecuteAsync(SheetsService sheetsService, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: Find the row containing the entity with the matching key
        var rowIndex = await FindRowByKeyAsync(sheetsService, cancellationToken);

        if (rowIndex == -1)
        {
            throw new InvalidOperationException(
                $"Entity with key '{keyValue}' not found in sheet '{sheetName}' for update.");
        }

        // Step 2: Update the specific row
        await UpdateRowAsync(sheetsService, rowIndex, cancellationToken);
    }

    private async Task<int> FindRowByKeyAsync(SheetsService sheetsService, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get all data from the sheet to find the matching row
        var range = $"{sheetName}!A{startRow}:ZZ"; // Read from start row to end
        var request = sheetsService.Spreadsheets.Values.Get(sheetId, range);
        var response = await request.ExecuteAsync(cancellationToken);

        if (response.Values == null || response.Values.Count == 0)
        {
            return -1; // No data found
        }

        var dataStartIndex = hasHeader ? 1 : 0; // Skip header row if present
        var actualStartRow = startRow + dataStartIndex;

        // Search for the row with matching key value
        for (var i = dataStartIndex; i < response.Values.Count; i++)
        {
            var row = response.Values[i];

            // Check if the key column exists and matches
            if (row.Count > keyColumnIndex && row[keyColumnIndex] != null)
            {
                var cellValue = row[keyColumnIndex].ToString();
                var keyValueString = keyValue?.ToString();

                if (string.Equals(cellValue, keyValueString, StringComparison.OrdinalIgnoreCase))
                {
                    // Return the actual row number in the sheet (1-based)
                    return actualStartRow + i - dataStartIndex;
                }
            }
        }

        return -1; // Not found
    }

    private async Task UpdateRowAsync(SheetsService sheetsService, int rowIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Create the specific range for this row
            var range = $"{sheetName}!A{rowIndex}:{maxColumnLetter}{rowIndex}";

            var valueRange = new ValueRange
            {
                Values = new List<IList<object>> { values }
            };

            var request = sheetsService.Spreadsheets.Values.Update(valueRange, sheetId, range);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to update row {rowIndex} in sheet '{sheetName}'. " +
                $"Entity key: {keyPropertyName}={keyValue}. Error: {ex.Message}", ex);
        }
    }
}