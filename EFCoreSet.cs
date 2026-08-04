using System;
using System.Collections.Generic;
using System.Linq;
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
			if (entity == null) throw new ArgumentNullException(nameof(entity));

			if (!IsAddable(entity)) return;

			this.NativeQuery.Add(entity);
		}

		/// <inheritdoc/>
		public void AddRange(IEnumerable<E> entities)
		{
			if (entities == null) throw new ArgumentNullException(nameof(entities));

			this.NativeQuery.AddRange(entities.Where(IsAddable));
		}

		/// <inheritdoc/>
		public void Attach(E entity)
		{
			this.NativeQuery.Attach(entity);
		}

		/// <inheritdoc/>
		public E Create()
		{
			return this.DomainContainer.Create<E>();
		}

		/// <inheritdoc/>
		public T Create<T>() where T : class, E
		{
			return this.DomainContainer.Create<T>();
		}

		/// <inheritdoc/>
		public E Find(params object[] keys)
		{
			return this.NativeQuery.Find(keys);
		}

		/// <inheritdoc/>
		public void Remove(E entity)
		{
			this.NativeQuery.Remove(entity);
		}

		/// <inheritdoc/>
		public void RemoveRange(IEnumerable<E> entities)
		{
			this.NativeQuery.RemoveRange(entities);
		}

		#endregion

		#region Private methods

		/// <summary>
		/// Determines whether an entity still has to be handed to the underlying set in order to be added.
		/// </summary>
		/// <param name="entity">The entity being added.</param>
		/// <returns>Returns true when the entity is not tracked yet.</returns>
		/// <exception cref="DataAccessException">Thrown when the entity is being deleted.</exception>
		/// <remarks>
		/// Adding an entity which is already tracked must not change anything. With nested transaction scopes
		/// an entity may well have been stored by an inner commit before control returns to the caller which
		/// adds it, and that caller has no way of knowing. Entity Framework Core would otherwise turn such an
		/// entity back to 'added' while it keeps its stored key, and attempt to insert it a second time.
		/// </remarks>
		private bool IsAddable(E entity)
		{
			var state = this.DomainContainer.Entry(entity).State;

			if (state == TrackingState.Deleted)
			{
				throw new DataAccessException(
					$"Cannot add an entity of type '{typeof(E).FullName}' while it is being deleted: " +
					"whether that abandons the deletion or stores a second entity is not defined.");
			}

			return state == TrackingState.Detached;
		}

		#endregion
	}
}
