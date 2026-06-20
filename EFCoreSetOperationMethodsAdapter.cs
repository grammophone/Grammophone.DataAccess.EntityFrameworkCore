using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core implementation of set-based terminal mutation methods.
	/// </summary>
	public class EFCoreSetOperationMethodsAdapter : SetOperationMethodsAdapter
	{
		#region Public methods

		/// <inheritdoc/>
		public override int ExecuteDelete<T>(IQueryable<T> nativeQuery)
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));

			return nativeQuery.ExecuteDelete();
		}

		/// <inheritdoc/>
		public override Task<int> ExecuteDeleteAsync<T>(
			IQueryable<T> nativeQuery,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));

			return nativeQuery.ExecuteDeleteAsync(cancellationToken);
		}

		#endregion
	}
}
