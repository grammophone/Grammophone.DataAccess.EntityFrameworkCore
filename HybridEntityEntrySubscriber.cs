using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
#pragma warning disable EF1001 // Internal EF Core API usage.

	/// <summary>
	/// Per-instance hybrid entity entry subscriber.
	/// When the entity type is configured for change-notification tracking
	/// but the instance does not implement the required interface (e.g.
	/// <see cref="INotifyPropertyChanging"/>), falls back to Snapshot-style
	/// behaviour for that instance instead of throwing.
	/// Entities that already implement the interface are subscribed normally.
	/// </summary>
	public class HybridEntityEntrySubscriber : InternalEntityEntrySubscriber
	{
		/// <inheritdoc/>
		public override bool SnapshotAndSubscribe(InternalEntityEntry entry)
		{
			var entityType = (IEntityType)entry.EntityType;
			var strategy = ((IReadOnlyTypeBase)entityType).GetChangeTrackingStrategy();

			if (RequiresSnapshotFallback(strategy, entry.Entity))
			{
				foreach (var navigation in entityType.GetNavigations())
				{
					if (navigation.IsCollection)
						SubscribeCollectionChanged(entry, navigation);
				}

				foreach (var skipNavigation in entityType.GetSkipNavigations())
				{
					SubscribeCollectionChanged(entry, skipNavigation);
				}

				return true;
			}

			return base.SnapshotAndSubscribe(entry);
		}

		/// <inheritdoc/>
		public override void Unsubscribe(InternalEntityEntry entry)
		{
			var entityType = (IEntityType)entry.EntityType;
			var strategy = ((IReadOnlyTypeBase)entityType).GetChangeTrackingStrategy();

			if (RequiresSnapshotFallback(strategy, entry.Entity))
			{
				foreach (var navigation in entityType.GetNavigations())
				{
					if (navigation.IsCollection)
						UnsubscribeCollectionChanged(entry, navigation);
				}

				foreach (var skipNavigation in entityType.GetSkipNavigations())
				{
					UnsubscribeCollectionChanged(entry, skipNavigation);
				}
			}
			else
			{
				base.Unsubscribe(entry);
			}
		}

		private static bool RequiresSnapshotFallback(
			ChangeTrackingStrategy strategy,
			object entity)
		{
			switch (strategy)
			{
				case ChangeTrackingStrategy.ChangedNotifications:
					return !(entity is INotifyPropertyChanged);

				case ChangeTrackingStrategy.ChangingAndChangedNotifications:
				case ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues:
					return !(entity is INotifyPropertyChanging);

				default:
					return false;
			}
		}
	}

#pragma warning restore EF1001
}
