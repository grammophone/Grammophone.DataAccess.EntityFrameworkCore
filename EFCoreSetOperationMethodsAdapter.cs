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
		/// <remarks>
		/// Delegates to Entity Framework Core <c>ExecuteDelete</c>, which executes a set-based delete without materializing
		/// entities and without synchronizing already tracked entities.
		/// </remarks>
		public override int ExecuteDelete<T>(IQueryable<T> nativeQuery)
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));

			return nativeQuery.ExecuteDelete();
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Delegates to Entity Framework Core <c>ExecuteDeleteAsync</c>, which executes a set-based delete without materializing
		/// entities and without synchronizing already tracked entities.
		/// </remarks>
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
