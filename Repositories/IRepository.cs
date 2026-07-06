using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Repositories;

public interface IRepository<T> where T : AuditableEntity
{
    IQueryable<T> Query();
    Task<T?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Update(T entity);
    void SoftDelete(T entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class EfRepository<T>(IncentiveDbContext db) : IRepository<T> where T : AuditableEntity
{
    public IQueryable<T> Query() => db.Set<T>().AsQueryable();
    public Task<T?> GetAsync(int id, CancellationToken cancellationToken = default) => db.Set<T>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) => await db.Set<T>().AddAsync(entity, cancellationToken);
    public void Update(T entity) => db.Set<T>().Update(entity);
    public void SoftDelete(T entity)
    {
        entity.IsDeleted = true;
        db.Set<T>().Update(entity);
    }
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
