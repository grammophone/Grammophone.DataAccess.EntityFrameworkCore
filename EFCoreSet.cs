using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// An <see cref="IEntitySet{E}"/> implementation based on Entity Framework Core's <see cref="DbSet{TEntity}"/>.
	/// </summary>
	/// <typeparam name="E">The type of the entities.</typeparam>
	public class EFCoreSet<E> : EFCoreQuery<E, DbSet<E>>, IEntitySet<E>
		where E : class
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="dbSet">The Entity Framework Core set.</param>
		/// <param name="domainContainer">The domain container which the query pertains to.</param>
		public EFCoreSet(DbSet<E> dbSet, IDomainContainer domainContainer)
			: base(dbSet, domainContainer)
		{
		}

		#endregion

		#region IEntitySet<E> implementation

		/// <inheritdoc/>
		public void Add(E entity)
		{
			NativeQuery.Add(entity);
		}

		/// <inheritdoc/>
		public void AddRange(IEnumerable<E> entities)
		{
			NativeQuery.AddRange(entities);
		}

		/// <inheritdoc/>
		public void Attach(E entity)
		{
			NativeQuery.Attach(entity);
		}

		/// <inheritdoc/>
		public E Create()
		{
			return DomainContainer.Create<E>();
		}

		/// <inheritdoc/>
		public T Create<T>() where T : class, E
		{
			return DomainContainer.Create<T>();
		}

		/// <inheritdoc/>
		public E Find(params object[] keys)
		{
			return NativeQuery.Find(keys);
		}

		/// <inheritdoc/>
		public void Remove(E entity)
		{
			NativeQuery.Remove(entity);
		}

		/// <inheritdoc/>
		public void RemoveRange(IEnumerable<E> entities)
		{
			NativeQuery.RemoveRange(entities);
		}

		#endregion
	}
}
