using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Slick_Domain.Interfaces;
using Slick_Domain.Models;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Data.Entity.Infrastructure;
using Slick_Domain.Common;

namespace Slick_Domain.Services
{
 
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        private readonly SlickContext context;
        private readonly DbSet<TEntity> _dbSet;
        public Repository(SlickContext p_context)
        {           
            context = p_context;
            _dbSet = context.Set<TEntity>();
        }

        public IQueryable<TEntity> GetAll
        {
            get
            {
                return _dbSet;
            }
        }

        public IQueryable<TEntity> AllAsQuery
        {
            get
            {
                return _dbSet;
            }
        }

        public IQueryable<TEntity> AllAsQueryNoTracking
        {
            get
            {
                return _dbSet.AsNoTracking();
            }
        }

        public void Add(TEntity entity)
        {
            _dbSet.Add(entity);
            context.SaveChanges();
        }

        //Eager Loading using Includes
        public IQueryable<TEntity> AllIncluding(params Expression<Func<TEntity, object>>[] includeProperties)
        {
            IQueryable<TEntity> query = _dbSet.AsNoTracking();
            foreach (var includeProperty in includeProperties)
            {
                query = query.Include(includeProperty);
            }
            return query;
        }

        public void AddAll(IEnumerable<TEntity> items)
        {
            _dbSet.AddRange(items);
            context.SaveChanges();
        }

        public void DeleteAll(IEnumerable<TEntity> items)
        {
            try
            { 
                _dbSet.RemoveRange(items);
                context.SaveChanges();
            }
            catch (DbUpdateException e)
            {
                var baseException = e.InnerException.GetBaseException();
                if (baseException.GetType() == typeof(SqlException))
                {
                    SqlException error = baseException as SqlException;
                    if (error.Number == 547)
                    {
                        throw new Exception($"{DomainConstants.ForeignKeyConstraint }{error.Message}");
                    }
                    else throw;
                }
                else throw;
            }
        }


        public void Delete(int id)
        {
            var entityToDelete = _dbSet.Find(id);
            try
            {
                _dbSet.Remove(entityToDelete);
                context.SaveChanges();
            }
            catch (DbUpdateException e)
            {
                var baseException = e.InnerException.GetBaseException();
                if (baseException.GetType() == typeof(SqlException))
                {
                    SqlException error = baseException as SqlException;
                    if (error.Number == 547)
                    {
                        throw new Exception(DomainConstants.ForeignKeyConstraint);
                    }
                }
                else throw;
            }
        }

        public void Dispose()
        {
            context.Dispose();
        }

        public TEntity FindById(int id)
        {
            return _dbSet.Find(id);
        }

        public async Task<List<TEntity>> GetItemsAsync()
        {
            return await _dbSet.ToListAsync();
        }


        public async Task<List<TEntity>> GetItemsAsyncNoTracking()
        {
            return await _dbSet.AsNoTracking().ToListAsync();
        }

        public IEnumerable<TEntity> ListWithPredicate(Func<TEntity, bool> whereClause)
        {
            return _dbSet.Where(whereClause).ToList();
        }

        public IEnumerable<TEntity> List()
        {
            return _dbSet.ToList();
        }

        public IEnumerable<TEntity> ListNoTracking()
        {
            return _dbSet.AsNoTracking().ToList();
        }

        public void Remove(TEntity entity)
        {
            try
            {
                _dbSet.Remove(entity);
                context.Entry(entity).State = EntityState.Deleted;
                context.SaveChanges();
            }
            catch (DbUpdateException e)
            {
                var baseException = e.InnerException.GetBaseException();
                if (baseException.GetType() == typeof(SqlException))
                {
                    SqlException error = baseException as SqlException;
                    if (error.Number == 547)
                    {
                        context.Entry(entity).Reload();
                        throw new Exception(DomainConstants.ForeignKeyConstraint);
                    }
                }
                else throw;
            }            
        }

        public void Update(TEntity entity)
        {
            _dbSet.Attach(entity);
            context.Entry(entity).State = EntityState.Modified;
            context.SaveChanges();
        }



    }
}
