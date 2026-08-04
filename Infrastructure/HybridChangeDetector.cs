using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Grammophone.DataAccess.EntityFrameworkCore.Infrastructure
{
#pragma warning disable EF1001 // Internal EF Core API usage.

	/// <summary>
	/// Per-instance hybrid change detector.
	/// When a tracked entity implements <see cref="INotifyPropertyChanged"/> (e.g. a change-tracking
	/// proxy or <see cref="DataAccess.ManyToMany{TLeft,TRight}"/>), snapshot property comparison is
	/// skipped — property changes are already captured by the event.
	/// Entities that do not implement <see cref="INotifyPropertyChanged"/> fall back to the standard
	/// Snapshot-style comparison (property + navigation detection).
	/// </summary>
	public class HybridChangeDetector : ChangeDetector
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public HybridChangeDetector(
			IDiagnosticsLogger<DbLoggerCategory.ChangeTracking> logger,
			ILoggingOptions loggingOptions)
			: base(logger, loggingOptions)
		{
		}

		#endregion

		#region ChangeDetector overrides

		/// <inheritdoc/>
		public override void DetectChanges(IStateManager stateManager)
		{
			OnDetectingAllChanges(stateManager);

			var changesFound = false;

			foreach (var entry in stateManager.ToList())
			{
				switch (entry.EntityState)
				{
					case EntityState.Detached:
						break;

					case EntityState.Deleted:
						if (entry.SharedIdentityEntry != null)
						{
							continue;
						}

						goto default;

					default:
						if (DetectChangesCore(entry))
						{
							changesFound = true;
						}

						break;
				}
			}

			OnDetectedAllChanges(stateManager, changesFound);
		}

		/// <inheritdoc/>
		public override void DetectChanges(InternalEntityEntry entry)
		{
			DetectChangesCascade(entry, new HashSet<InternalEntityEntry>());
		}

		#endregion

		#region Private helpers

		private bool DetectChangesCascade(InternalEntityEntry entry, HashSet<InternalEntityEntry> visited)
		{
			if (entry.EntityState == EntityState.Detached)
			{
				return false;
			}

			if (!visited.Add(entry))
			{
				return false;
			}

			var changesFound = false;

			foreach (var foreignKey in entry.EntityType.GetForeignKeys())
			{
				var principalEntry = entry.StateManager.FindPrincipal(entry, foreignKey);

				if (principalEntry != null && !visited.Contains(principalEntry))
				{
					if (DetectChangesCascade(principalEntry, visited))
					{
						changesFound = true;
					}
				}
			}

			if (DetectChangesCore(entry))
			{
				changesFound = true;
			}

			return changesFound;
		}

		private bool DetectChangesCore(InternalEntityEntry entry)
		{
			if (entry.Entity is INotifyPropertyChanged)
			{
				// Proxied / notification entity — events capture all changes.
				return false;
			}

			// Unproxied instance of an entity type configured for notification tracking.
			// The base ChangeDetector refuses to look at it: its LocalDetectChanges returns
			// immediately whenever the entity type's strategy is not Snapshot, so a plain
			// 'new Entity()' would never have its property or navigation assignments noticed —
			// most visibly, an assigned reference navigation would never reach foreign key fixup.
			// Drive the detection explicitly through PropertyChanged, the same entry point a
			// change-tracking proxy's notification would use.
			base.DetectChanges(entry);

			return DetectSnapshotChanges(entry);
		}

		/// <summary>
		/// Applies Snapshot-style detection to a single entry by comparing it against the snapshots
		/// taken in <see cref="HybridEntityEntrySubscriber"/> and reporting the differences through
		/// the notification path.
		/// </summary>
		private bool DetectSnapshotChanges(InternalEntityEntry entry)
		{
			var entityType = entry.EntityType;

			if (entityType.GetChangeTrackingStrategy() == ChangeTrackingStrategy.Snapshot)
			{
				// The base ChangeDetector has already handled this entry.
				return false;
			}

			var changesFound = false;

			if (entry.EntityState != EntityState.Added && entry.EntityState != EntityState.Deleted)
			{
				foreach (var property in entityType.GetProperties())
				{
					if (property.GetOriginalValueIndex() < 0) continue;
					if (entry.IsModified(property) || entry.IsConceptualNull(property)) continue;

					if (HasValueChanged(entry, property))
					{
						PropertyChanged(entry, property, setModified: true);

						changesFound = true;
					}
				}
			}

			if (entry.HasRelationshipSnapshot)
			{
				// PropertyChanged routes navigations to DetectNavigationChange, which compares the
				// current value against the relationship snapshot and only acts on a real difference.
				foreach (var navigation in entityType.GetNavigations())
				{
					PropertyChanged(entry, navigation, setModified: false);
				}

				foreach (var skipNavigation in entityType.GetSkipNavigations())
				{
					PropertyChanged(entry, skipNavigation, setModified: false);
				}

				changesFound = true;
			}

			return changesFound;
		}

		private static bool HasValueChanged(InternalEntityEntry entry, IProperty property)
		{
			var currentValue = entry.GetCurrentValue(property);
			var originalValue = entry.GetOriginalValue(property);

			var comparer = property.GetValueComparer();

			return comparer != null
				? !comparer.Equals(currentValue, originalValue)
				: !Equals(currentValue, originalValue);
		}

		#endregion
	}

#pragma warning restore EF1001
}
