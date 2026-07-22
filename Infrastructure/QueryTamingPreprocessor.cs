using System;
using System.Linq.Expressions;
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
	}
}
