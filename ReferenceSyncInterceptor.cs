using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;


namespace Grammophone.DataAccess.EntityFrameworkCore
{
#pragma warning disable EF1001 // Suppress Internal EF Core API warning

	/// <summary>
	/// Attempts to match materialized objects to previously instanciated objects for the same entity, if there are any.
	/// </summary>
	public class ReferenceSyncInterceptor : IMaterializationInterceptor
	{
		/// <inheritdoc/>
		public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
		{
			var context = materializationData.Context;
			if (context == null) return instance;

			var entityType = materializationData.EntityType;
			var primaryKey = entityType.FindPrimaryKey();
			if (primaryKey == null) return instance;

			var keyValues = primaryKey.Properties.Select(p => p.GetGetter().GetClrValue(instance)).ToArray();
			var stateManager = context.GetService<IStateManager>();
			var internalEntry = stateManager.TryGetEntry(primaryKey, keyValues);

			if (internalEntry != null && internalEntry.Entity != null)
			{
				return internalEntry.Entity;
			}

			return instance;

		}
	}
#pragma warning restore EF1001
}
