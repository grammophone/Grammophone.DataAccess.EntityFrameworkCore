using Microsoft.EntityFrameworkCore;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Conversions between Grammophone and Entity Framework Core types.
	/// </summary>
	public static class TypeConversions
	{
		#region Public methods

		/// <summary>
		/// Convert a <see cref="TrackingState"/> value to <see cref="EntityState"/>.
		/// </summary>
		public static EntityState TrackingStateToEntityState(TrackingState trackingState)
		{
			EntityState entityState = default;

			if ((trackingState & TrackingState.Detached) != 0) entityState |= EntityState.Detached;
			if ((trackingState & TrackingState.Unchanged) != 0) entityState |= EntityState.Unchanged;
			if ((trackingState & TrackingState.Added) != 0) entityState |= EntityState.Added;
			if ((trackingState & TrackingState.Deleted) != 0) entityState |= EntityState.Deleted;
			if ((trackingState & TrackingState.Modified) != 0) entityState |= EntityState.Modified;

			return entityState;
		}

		/// <summary>
		/// Convert an <see cref="EntityState"/> value to <see cref="TrackingState"/>.
		/// </summary>
		public static TrackingState EntityStateToTrackingState(EntityState entityState)
		{
			TrackingState trackingState = default;

			if ((entityState & EntityState.Detached) != 0) trackingState |= TrackingState.Detached;
			if ((entityState & EntityState.Unchanged) != 0) trackingState |= TrackingState.Unchanged;
			if ((entityState & EntityState.Added) != 0) trackingState |= TrackingState.Added;
			if ((entityState & EntityState.Deleted) != 0) trackingState |= TrackingState.Deleted;
			if ((entityState & EntityState.Modified) != 0) trackingState |= TrackingState.Modified;

			return trackingState;
		}

		#endregion
	}
}
