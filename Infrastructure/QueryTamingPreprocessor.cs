using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Grammophone.DataAccess.EntityFrameworkCore.Infrastructure
{
	/// <summary>
	/// Factory for creating <see cref="QueryTamingPreprocessor"/> instances.
	/// </summary>
	public class QueryTamingPreprocessorFactory : IQueryTranslationPreprocessorFactory
	{
		private readonly QueryTranslationPreprocessorDependencies _dependencies;

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="dependencies">The EF Core query translation preprocessor dependencies.</param>
		public QueryTamingPreprocessorFactory(QueryTranslationPreprocessorDependencies dependencies)
		{
			_dependencies = dependencies;
		}

		/// <inheritdoc/>
		public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
		{
			return new QueryTamingPreprocessor(_dependencies, queryCompilationContext);
		}
	}

	/// <summary>
	/// A <see cref="QueryTranslationPreprocessor"/> that tolerates <c>IOrderedQueryable</c>
	/// and <c>IQueryable</c> projections in anonymous types produced by <c>let</c> clauses.
	/// EF Core's normalizer rejects these as invalid, but they are valid SQL that works in EF6.
	/// When the normalizer throws, this preprocessor falls back to <c>ProcessQueryRoots</c>.
	/// </summary>
	public class QueryTamingPreprocessor : QueryTranslationPreprocessor
	{
		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="dependencies">The EF Core query translation preprocessor dependencies.</param>
		/// <param name="queryCompilationContext">The query compilation context.</param>
		public QueryTamingPreprocessor(
			QueryTranslationPreprocessorDependencies dependencies,
			QueryCompilationContext queryCompilationContext)
			: base(dependencies, queryCompilationContext)
		{
		}

		/// <inheritdoc/>
		public override Expression NormalizeQueryableMethod(Expression expression)
		{
			try
			{
				return base.NormalizeQueryableMethod(expression);
			}
			catch (InvalidOperationException ex) when (ex.Message.Contains("Collections in the final projection must be an 'IEnumerable<T>'"))
			{
				return ProcessQueryRoots(expression);
			}
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Entity Framework Core refuses eager loading on a query whose source is a projection through a
		/// navigation, such as <c>from t in Transitions select t.Event</c> followed by an <c>Include</c>.
		/// EF6 accepts that shape, so rather than fail the query, retry it without the eager loading:
		/// with lazy loading proxies the navigations are still resolved when they are read, only in
		/// separate round trips. Queries carrying no eager loading are left to fail as before.
		/// </remarks>
		public override Expression Process(Expression query)
		{
			try
			{
				return base.Process(query);
			}
			catch (InvalidOperationException ex) when (IsEagerLoadingRejection(ex))
			{
				var queryWithoutEagerLoading = new EagerLoadingStrippingExpressionVisitor().Visit(query);

				// Nothing was stripped, so retrying would fail identically.
				if (queryWithoutEagerLoading == query) throw;

				return base.Process(queryWithoutEagerLoading);
			}
		}

		private static bool IsEagerLoadingRejection(InvalidOperationException exception)
			=> exception.Message.Contains("'Include'") || exception.Message.Contains("'ThenInclude'");

		/// <summary>
		/// Removes <see cref="EntityFrameworkQueryableExtensions"/> eager loading calls from a query,
		/// replacing each of them by the query they were applied to.
		/// </summary>
		private sealed class EagerLoadingStrippingExpressionVisitor : ExpressionVisitor
		{
			protected override Expression VisitMethodCall(MethodCallExpression node)
			{
				if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
					&& (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include)
						|| node.Method.Name == nameof(EntityFrameworkQueryableExtensions.ThenInclude)))
				{
					return Visit(node.Arguments[0]);
				}

				return base.VisitMethodCall(node);
			}
		}
	}
}
