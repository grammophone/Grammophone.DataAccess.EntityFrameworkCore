using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Implementation of <see cref="IChangeTracker"/> for Entity Framework Core.
	/// </summary>
	public class EFCoreChangeTracker : IChangeTracker
	{
		#region Private fields

		private readonly DbContext dbContext;

		#endregion

		#region Construction

		internal EFCoreChangeTracker(DbContext dbContext)
		{
			if (dbContext == null) throw new ArgumentNullException(nameof(dbContext));

			this.dbContext = dbContext;
		}

		#endregion

		#region IChangeTracker implementation

		/// <inheritdoc/>
		public void DetectChanges() => dbContext.ChangeTracker.DetectChanges();

		/// <inheritdoc/>
		public bool HasChanges() => dbContext.ChangeTracker.HasChanges();

		/// <inheritdoc/>
		public IEnumerable<IEntityEntry<object>> Entries()
			=> dbContext.ChangeTracker.Entries<object>().Select(e => new EFCoreEntityEntry<object>(e));

		/// <inheritdoc/>
		public IEnumerable<IEntityEntry<E>> Entries<E>() where E : class
			=> dbContext.ChangeTracker.Entries<E>().Select(e => new EFCoreEntityEntry<E>(e));

		/// <inheritdoc/>
		public IEnumerable<IEntityEntry<object>> Entries(TrackingState trackingState)
		{
			var entityState = TypeConversions.TrackingStateToEntityState(trackingState);

			return dbContext.ChangeTracker.Entries<object>()
				.Where(e => e.State == entityState)
				.Select(e => new EFCoreEntityEntry<object>(e));
		}

		/// <inheritdoc/>
		public IEnumerable<IEntityEntry<E>> Entries<E>(TrackingState trackingState) where E : class
		{
			var entityState = TypeConversions.TrackingStateToEntityState(trackingState);

			return dbContext.ChangeTracker.Entries<E>()
				.Where(e => e.State == entityState)
				.Select(e => new EFCoreEntityEntry<E>(e));
		}

		/// <inheritdoc/>
		public void UndoChanges()
		{
			foreach (var entry in dbContext.ChangeTracker.Entries().ToArray())
			{
				switch (entry.State)
				{
					case EntityState.Added:
						entry.State = EntityState.Detached;
						break;

					case EntityState.Modified:
						entry.CurrentValues.SetValues(entry.OriginalValues);
						entry.State = EntityState.Unchanged;
						break;

					case EntityState.Deleted:
						entry.State = EntityState.Unchanged;
						break;
				}
			}
		}

		#endregion
	}
}
