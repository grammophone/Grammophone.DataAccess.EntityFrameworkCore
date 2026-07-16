using System;
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
			switch (trackingState)
			{
				case TrackingState.Detached: return EntityState.Detached;
				case TrackingState.Unchanged: return EntityState.Unchanged;
				case TrackingState.Added: return EntityState.Added;
				case TrackingState.Deleted: return EntityState.Deleted;
				case TrackingState.Modified: return EntityState.Modified;
				default: return EntityState.Detached;
			}
		}

		/// <summary>
		/// Convert an <see cref="EntityState"/> value to <see cref="TrackingState"/>.
		/// </summary>
		public static TrackingState EntityStateToTrackingState(EntityState entityState)
		{
			switch (entityState)
			{
				case EntityState.Detached: return TrackingState.Detached;
				case EntityState.Unchanged: return TrackingState.Unchanged;
				case EntityState.Added: return TrackingState.Added;
				case EntityState.Deleted: return TrackingState.Deleted;
				case EntityState.Modified: return TrackingState.Modified;
				default: return TrackingState.Detached;
			}
		}

		#endregion
	}
}
