using System.Collections.Generic;

namespace FopFinance.Repositories
{
    /// <summary>
    /// Generic read/write repository contract for in-memory collections.
    /// </summary>
    public interface IRepository<T>
    {
        IReadOnlyList<T> GetAll();
        void Add(T entity);
        bool Remove(string id);
        void ReplaceAll(IEnumerable<T> entities);
    }
}
