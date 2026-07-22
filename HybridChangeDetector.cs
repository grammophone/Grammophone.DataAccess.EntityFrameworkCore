using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grammophone.DataAccess.EntityFrameworkCore
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

			// Unproxied — delegate to base ChangeDetector which handles all navigation types.
			base.DetectChanges(entry);

			return true;
		}

		#endregion
	}

#pragma warning restore EF1001
}
