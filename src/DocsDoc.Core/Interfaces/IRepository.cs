using System;
using System.Collections.Generic;

namespace DocsDoc.Core.Interfaces
{
    /// <summary>
    /// Generic repository interface for CRUD operations on entities.
    /// </summary>
    public interface IRepository<T>
    {
        T Get(Guid id);
        IEnumerable<T> GetAll();
        void Add(T entity);
        void Update(T entity);
        void Delete(Guid id);
    }
} 