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
			=> dbContext.ChangeTracker.Entries<object>()
				.Where(e => Matches(e.State, trackingState))
				.Select(e => new EFCoreEntityEntry<object>(e));

		/// <inheritdoc/>
		public IEnumerable<IEntityEntry<E>> Entries<E>(TrackingState trackingState) where E : class
			=> dbContext.ChangeTracker.Entries<E>()
				.Where(e => Matches(e.State, trackingState))
				.Select(e => new EFCoreEntityEntry<E>(e));

		/// <summary>
		/// Determines whether an entry's state is among the requested ones.
		/// </summary>
		/// <param name="entityState">The state Entity Framework Core reports for the entry.</param>
		/// <param name="trackingState">The requested states, which may be a combination.</param>
		/// <remarks>
		/// <para>
		/// The comparison runs in <see cref="TrackingState"/> rather than in
		/// <see cref="EntityState"/> because only the former is expressed as flags.
		/// <see cref="TrackingState"/> assigns powers of two and callers combine them —
		/// <c>Added | Unchanged | Modified</c> is the view of "everything the context holds that is
		/// not deleted". Entity Framework Core's <see cref="EntityState"/> is a sequential
		/// enumeration, so a combination has no meaning there: converting one yields a single
		/// unrelated value, and comparing that for equality matches nothing.
		/// </para>
		/// <para>
		/// Converting the entry's own state instead is well defined in both directions, and testing
		/// it bitwise answers the question actually being asked: is this entry in <i>any</i> of the
		/// requested states. This mirrors the Entity Framework 6 implementation, which reaches the
		/// same semantics by casting — its <see cref="EntityState"/> is itself a flags enumeration
		/// with matching values, which is why it needs no equivalent of this method.
		/// </para>
		/// </remarks>
		private static bool Matches(EntityState entityState, TrackingState trackingState)
			=> (TypeConversions.EntityStateToTrackingState(entityState) & trackingState) != 0;

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
