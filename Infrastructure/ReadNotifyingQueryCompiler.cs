using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Grammophone.DataAccess.EntityFrameworkCore.Infrastructure
{
#pragma warning disable EF1001 // Internal EF Core API usage.

	/// <summary>
	/// An <see cref="IQueryCompiler"/> that defers <see cref="IEntityListener.OnRead(object)"/> notifications
	/// until each produced query result has been fully materialized — including its eager-loaded navigations —
	/// while the underlying data reader is still open.
	/// </summary>
	/// <remarks>
	/// <para>
	/// EF Core raises <c>ChangeTracker.Tracked</c> for an entity <b>before</b> the shaper fixes up that row's
	/// <c>Include</c>d navigations, so notifying a listener from there dereferences a navigation that is not
	/// loaded yet. <see cref="EFCoreDomainContainer"/> therefore only <i>buffers</i> reads as entities are
	/// tracked or materialized; this compiler flushes the buffer once each result element is shaped (root and
	/// its includes) but before it reaches the caller. Buffering — rather than notifying the root alone — is
	/// what lets included entities be notified too, since they never appear in the result sequence.
	/// </para>
	/// <para>
	/// The flush runs while the reader is still open, so a read whose query omits the navigation the listener
	/// needs still lazy-loads into that open reader and fails — the intended N+1 signal is preserved — and a
	/// listener that rejects an entity still aborts the query before the caller observes it.
	/// </para>
	/// </remarks>
	public class ReadNotifyingQueryCompiler : QueryCompiler
	{
		#region Private fields

		private readonly ICurrentDbContext currentContext;

		private static readonly MethodInfo wrapEnumerableMethod =
			typeof(ReadNotifyingQueryCompiler).GetMethod(nameof(WrapEnumerable), BindingFlags.NonPublic | BindingFlags.Static);

		private static readonly MethodInfo wrapAsyncEnumerableMethod =
			typeof(ReadNotifyingQueryCompiler).GetMethod(nameof(WrapAsyncEnumerable), BindingFlags.NonPublic | BindingFlags.Static);

		private static readonly MethodInfo interceptAndNotifyAsyncMethod =
			typeof(ReadNotifyingQueryCompiler).GetMethod(nameof(InterceptAndNotifyAsync), BindingFlags.NonPublic | BindingFlags.Static);

		private static readonly ConcurrentDictionary<MethodInfo, ConcurrentDictionary<Type, MethodInfo>> closedMethodCache =
			new ConcurrentDictionary<MethodInfo, ConcurrentDictionary<Type, MethodInfo>>();

		#endregion

		#region Construction

		/// <summary>
		/// Create. The parameters are supplied by Entity Framework Core's service provider.
		/// </summary>
		public ReadNotifyingQueryCompiler(
			IQueryContextFactory queryContextFactory,
			ICompiledQueryCache compiledQueryCache,
			ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
			IDatabase database,
			IDiagnosticsLogger<DbLoggerCategory.Query> logger,
			ICurrentDbContext currentContext,
			IEvaluatableExpressionFilter evaluatableExpressionFilter,
			IModel model)
			: base(
				queryContextFactory,
				compiledQueryCache,
				compiledQueryCacheKeyGenerator,
				database,
				logger,
				currentContext,
				evaluatableExpressionFilter,
				model)
		{
			this.currentContext = currentContext;
		}

		#endregion

		#region Public methods

		/// <inheritdoc/>
		public override TResult Execute<TResult>(Expression query)
		{
			var result = base.Execute<TResult>(query);

			var container = this.currentContext.Context as EFCoreDomainContainer;

			if (container == null) return result;

			if (TryGetGenericArgument(typeof(TResult), typeof(IEnumerable<>), out var elementType))
			{
				object wrappedSequence = Close(wrapEnumerableMethod, elementType).Invoke(null, new object[] { result, container });

				return (TResult)wrappedSequence;
			}

			container.NotifyForReadEntities();

			return result;
		}

		/// <inheritdoc/>
		public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
		{
			var result = base.ExecuteAsync<TResult>(query, cancellationToken);

			var container = this.currentContext.Context as EFCoreDomainContainer;

			if (container == null) return result;

			if (TryGetGenericArgument(typeof(TResult), typeof(IAsyncEnumerable<>), out var elementType))
			{
				object wrappedSequence = Close(wrapAsyncEnumerableMethod, elementType)
					.Invoke(null, new object[] { result, container, cancellationToken });

				return (TResult)wrappedSequence;
			}

			if (TryGetGenericArgument(typeof(TResult), typeof(Task<>), out var taskValueType))
			{
				object wrappedTask = Close(interceptAndNotifyAsyncMethod, taskValueType).Invoke(null, new object[] { result, container });

				return (TResult)wrappedTask;
			}

			return result;
		}

		#endregion

		#region Private methods

		private static async Task<T> InterceptAndNotifyAsync<T>(Task<T> sourceTask, EFCoreDomainContainer container)
		{
			// Await the genuine database query processing to complete first.
			// At this point, EF Core has finished object parsing and structural relationship fix-ups.
			T finalResult = await sourceTask.ConfigureAwait(false);

			// Call your notification trigger seamlessly right before control yields back
			container.NotifyForReadEntities();

			return finalResult;
		}

		private static IEnumerable<T> WrapEnumerable<T>(IEnumerable<T> source, EFCoreDomainContainer container)
		{
			foreach (var item in source)
			{
				container.NotifyForReadEntities();

				yield return item;
			}

			container.NotifyForReadEntities();
		}

		private static async IAsyncEnumerable<T> WrapAsyncEnumerable<T>(
			IAsyncEnumerable<T> source,
			EFCoreDomainContainer container,
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
			{
				container.NotifyForReadEntities();

				yield return item;
			}

			container.NotifyForReadEntities();
		}

		/// <summary>
		/// Extracts the single type argument of <paramref name="resultType"/> when it is a closed generic of
		/// <paramref name="openGenericType"/> (for example <c>IAsyncEnumerable&lt;&gt;</c>), so the wrapper can be
		/// closed over the exact element type — including an <c>internal</c> anonymous projection type that a
		/// <c>dynamic</c> call site could not bind.
		/// </summary>
		private static bool TryGetGenericArgument(Type resultType, Type openGenericType, out Type argument)
		{
			if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == openGenericType)
			{
				argument = resultType.GetGenericArguments()[0];

				return true;
			}

			argument = null;

			return false;
		}

		private static MethodInfo Close(MethodInfo openMethod, Type typeArgument)
		{
			var byTypeArgument = closedMethodCache.GetOrAdd(openMethod, _ => new ConcurrentDictionary<Type, MethodInfo>());

			return byTypeArgument.GetOrAdd(typeArgument, t => openMethod.MakeGenericMethod(t));
		}

		#endregion
	}

#pragma warning restore EF1001
}
