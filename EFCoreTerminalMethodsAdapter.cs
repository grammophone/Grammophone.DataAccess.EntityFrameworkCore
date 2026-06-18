using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core terminal methods adapter.
	/// </summary>
	public class EFCoreTerminalMethodsAdapter : DefaultTerminalMethodsAdapter
	{
		#region Public methods

		/// <inheritdoc/>
		public override Task<bool> AllAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> AllAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<bool> AllAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.AllAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<bool> AnyAsync<T>(IQueryable<T> query)
			=> AnyAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.AnyAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<bool> AnyAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> AnyAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<bool> AnyAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.AnyAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<int> CountAsync<T>(IQueryable<T> query)
			=> CountAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<int> CountAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> CountAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<int> CountAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.CountAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<long> LongCountAsync<T>(IQueryable<T> query)
			=> LongCountAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<long> LongCountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.LongCountAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<long> LongCountAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> LongCountAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<long> LongCountAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.LongCountAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> FirstAsync<T>(IQueryable<T> query)
			=> FirstAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> FirstAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.FirstAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> FirstAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> FirstAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> FirstAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.FirstAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query)
			=> FirstOrDefaultAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> FirstOrDefaultAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> FirstOrDefaultAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> SingleAsync<T>(IQueryable<T> query)
			=> SingleAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> SingleAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.SingleAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> SingleAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> SingleAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> SingleAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.SingleAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> SingleOrDefaultAsync<T>(IQueryable<T> query)
			=> SingleOrDefaultAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<T> SingleOrDefaultAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate)
			=> SingleOrDefaultAsync(query, predicate, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T> SingleOrDefaultAsync<T>(IQueryable<T> query, Expression<System.Func<T, bool>> predicate, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(query, predicate, cancellationToken);

		/// <inheritdoc/>
		public override Task<T[]> ToArrayAsync<T>(IQueryable<T> query)
			=> ToArrayAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<T[]> ToArrayAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.ToArrayAsync(query, cancellationToken);

		/// <inheritdoc/>
		public override Task<List<T>> ToListAsync<T>(IQueryable<T> query)
			=> ToListAsync(query, default(CancellationToken));

		/// <inheritdoc/>
		public override Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
			=> EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);

		#endregion
	}
}
