using Google.Apis.Sheets.v4;

namespace MyParadeManager.WebApi.GoogleSheets.EntityOperations;

public interface IEntityOperation
{
    Task ExecuteAsync(SheetsService sheetsService, CancellationToken cancellationToken = default);
}