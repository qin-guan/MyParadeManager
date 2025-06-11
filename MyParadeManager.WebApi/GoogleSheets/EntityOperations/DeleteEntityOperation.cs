using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace MyParadeManager.WebApi.GoogleSheets.EntityOperations;

public class DeleteEntityOperation<T>(
    T entity,
    string id,
    string sheetName,
    object keyValue,
    string keyPropertyName,
    int keyColumnIndex,
    bool hasHeader,
    int startRow
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
            throw new InvalidOperationException($"Entity with key '{keyValue}' not found in sheet '{sheetName}'.");
        }

        // Step 2: Delete the row
        await DeleteRowAsync(sheetsService, rowIndex, cancellationToken);
    }

    private async Task<int> FindRowByKeyAsync(SheetsService sheetsService, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get all data from the sheet to find the matching row
        var range = $"{sheetName}!A{startRow}:ZZ"; // Read from start row to end
        var request = sheetsService.Spreadsheets.Values.Get(id, range);
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

    private async Task DeleteRowAsync(SheetsService sheetsService, int rowIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // Get sheet metadata to find the sheet ID
            var spreadsheet = await sheetsService.Spreadsheets.Get(id).ExecuteAsync(cancellationToken);
            var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties.Title == sheetName);

            if (sheet == null)
            {
                throw new InvalidOperationException($"Sheet '{sheetName}' not found in spreadsheet.");
            }

            var sheetId = sheet.Properties.SheetId.Value;

            // Create delete dimension request
            var deleteRequest = new Request
            {
                DeleteDimension = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = sheetId,
                        Dimension = "ROWS",
                        StartIndex = rowIndex - 1, // Convert to 0-based index
                        EndIndex = rowIndex // End index is exclusive
                    }
                }
            };

            // Execute the batch update request
            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { deleteRequest }
            };

            await sheetsService.Spreadsheets.BatchUpdate(batchUpdateRequest, id)
                .ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to delete row {rowIndex} from sheet '{sheetName}'. " +
                $"Entity key: {keyPropertyName}={keyValue}. Error: {ex.Message}", ex);
        }
    }
}