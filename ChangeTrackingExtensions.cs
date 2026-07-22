using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
#pragma warning disable EF1001 // Internal EF Core API usage.

	/// <summary>
	/// Extension methods for configuring flexible change tracking.
	/// </summary>
	public static class ChangeTrackingExtensions
	{
		/// <summary>
		/// Registers per-instance hybrid change detection and entity entry subscription services,
		/// allowing both proxied and unproxied entities of the same type to be tracked together.
		/// Must be called after <c>UseChangeTrackingProxies</c> to take effect.
		/// </summary>
		public static DbContextOptionsBuilder UseFlexibleChangeTracking(this DbContextOptionsBuilder optionsBuilder)
		{
			((IDbContextOptionsBuilderInfrastructure)optionsBuilder)
				.AddOrUpdateExtension(new FlexibleChangeTrackingOptionsExtension());

			return optionsBuilder;
		}
	}

#pragma warning restore EF1001
}
