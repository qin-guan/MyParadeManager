using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace MyParadeManager.WebApi.GoogleSheets.EntityOperations;

public class AddEntityOperation<T>(T entity, string sheetId, string range, List<object> values)
    : IEntityOperation where T : class
{
    private readonly T _entity = entity;

    public async Task ExecuteAsync(SheetsService sheetsService, CancellationToken cancellationToken = default)
    {
        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> { values }
        };

        var request = sheetsService.Spreadsheets.Values.Append(valueRange, sheetId, range);
        request.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

        await request.ExecuteAsync(cancellationToken);
    }
}