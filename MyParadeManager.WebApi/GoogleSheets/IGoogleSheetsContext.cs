namespace MyParadeManager.WebApi.GoogleSheets;

public interface IGoogleSheetsContext
{
    Task<IEnumerable<T>> GetAsync<T>(CancellationToken cancellationToken = default) where T : class, new();
    Task<T?> GetByKeyAsync<T>(object key, CancellationToken cancellationToken = default) where T : class, new();
    Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new();
    Task<T> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new();
    Task DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new();
    Task<int> SaveChangesAsync(CancellationToken cancellation = default);
}