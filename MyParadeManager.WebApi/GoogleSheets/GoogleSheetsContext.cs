using Google.Apis.Sheets.v4;
using MyParadeManager.WebApi.GoogleSheets.EntityOperations;

namespace MyParadeManager.WebApi.GoogleSheets;

public partial class GoogleSheetsContext(SheetsService sheetsService, GoogleSheetsConfiguration configuration)
    : IGoogleSheetsContext
{
    private readonly SheetsService _sheetsService = sheetsService;
    private readonly List<IEntityOperation> _pendingOperations = [];
    private readonly GoogleSheetsConfiguration _configuration = configuration;

    public async Task<int> SaveChangesAsync(CancellationToken cancellation = default)
    {
        var operationsCount = _pendingOperations.Count;

        foreach (var operation in _pendingOperations)
        {
            await operation.ExecuteAsync(_sheetsService, cancellation);
        }

        _pendingOperations.Clear();
        return operationsCount;
    }

    protected void AddPendingOperation(IEntityOperation operation)
    {
        _pendingOperations.Add(operation);
    }
}