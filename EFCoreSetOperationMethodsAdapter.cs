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
		public override int ExecuteDelete<T>(IDomainContainer domainContainer, IQueryable<T> nativeQuery)
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));

			try
			{
				return RelationalQueryableExtensions.ExecuteDelete(nativeQuery);
			}
			catch (SystemException ex) when (!(ex is OperationCanceledException))
			{
				throw TranslateException(domainContainer, ex);
			}
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Delegates to Entity Framework Core <c>ExecuteDeleteAsync</c>, which executes a set-based delete without materializing
		/// entities and without synchronizing already tracked entities.
		/// </remarks>
		public override Task<int> ExecuteDeleteAsync<T>(
			IDomainContainer domainContainer,
			IQueryable<T> nativeQuery,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));

			return ExecuteAsync(
				domainContainer,
				() => RelationalQueryableExtensions.ExecuteDeleteAsync(nativeQuery, cancellationToken));
		}

		/// <inheritdoc/>
		public override int ExecuteUpdate<T>(
			IDomainContainer domainContainer,
			IQueryable<T> nativeQuery,
			Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls)
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));
			if (setPropertyCalls == null) throw new ArgumentNullException(nameof(setPropertyCalls));

			try
			{
				return RelationalQueryableExtensions.ExecuteUpdate(
					nativeQuery,
					EFCoreSetPropertyCallsTranslator.Translate(setPropertyCalls));
			}
			catch (SystemException ex) when (!(ex is OperationCanceledException))
			{
				throw TranslateException(domainContainer, ex);
			}
		}

		/// <inheritdoc/>
		public override Task<int> ExecuteUpdateAsync<T>(
			IDomainContainer domainContainer,
			IQueryable<T> nativeQuery,
			Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setPropertyCalls,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));
			if (setPropertyCalls == null) throw new ArgumentNullException(nameof(setPropertyCalls));

			return ExecuteAsync(
				domainContainer,
				() => RelationalQueryableExtensions.ExecuteUpdateAsync(
					nativeQuery,
					EFCoreSetPropertyCallsTranslator.Translate(setPropertyCalls),
					cancellationToken));
		}

		#endregion

		#region Private methods

		private static async Task<int> ExecuteAsync(IDomainContainer domainContainer, Func<Task<int>> operation)
		{
			try
			{
				return await operation();
			}
			catch (SystemException ex) when (!(ex is OperationCanceledException))
			{
				throw TranslateException(domainContainer, ex);
			}
		}

		private static Exception TranslateException(IDomainContainer domainContainer, SystemException exception)
		{
			return domainContainer?.TranslateException(exception) ?? exception;
		}

		#endregion
	}
}
