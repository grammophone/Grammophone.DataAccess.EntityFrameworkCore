using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Grammophone.DataAccess.QueryExtensions;
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

			return RelationalQueryableExtensions.ExecuteDelete(nativeQuery);
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

			return RelationalQueryableExtensions.ExecuteDeleteAsync(nativeQuery, cancellationToken);
		}

		/// <inheritdoc/>
		public override int ExecuteUpdate<T>(
			IQueryable<T> nativeQuery,
			Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls)
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));
			if (setPropertyCalls == null) throw new ArgumentNullException(nameof(setPropertyCalls));

			return RelationalQueryableExtensions.ExecuteUpdate(
				nativeQuery,
				EFCoreSetPropertyCallsTranslator.Translate(setPropertyCalls));
		}

		/// <inheritdoc/>
		public override Task<int> ExecuteUpdateAsync<T>(
			IQueryable<T> nativeQuery,
			Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));
			if (setPropertyCalls == null) throw new ArgumentNullException(nameof(setPropertyCalls));

			return RelationalQueryableExtensions.ExecuteUpdateAsync(
				nativeQuery,
				EFCoreSetPropertyCallsTranslator.Translate(setPropertyCalls),
				cancellationToken);
		}

		#endregion
	}
}
