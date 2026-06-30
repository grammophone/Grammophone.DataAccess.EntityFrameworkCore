using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Non-generic implementation of <see cref="IEntityQuery"/> using Entity Framework Core.
	/// </summary>
	/// <typeparam name="Q">The type of the Entity Framework Core query object.</typeparam>
	public class EFCoreQuery<Q> : IEntityQuery
		where Q : IQueryable
	{
		#region Private fields

		private TranslatingQueryProvider translatingProvider;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="nativeQuery">The Entity Framework Core query object.</param>
		/// <param name="domainContainer">The domain container which the query pertains to.</param>
		public EFCoreQuery(Q nativeQuery, IDomainContainer domainContainer)
		{
			if (nativeQuery == null) throw new ArgumentNullException(nameof(nativeQuery));
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));

			this.NativeQuery = nativeQuery;
			this.DomainContainer = domainContainer;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The underlying Entity Framework Core query object.
		/// </summary>
		public Q NativeQuery { get; }

		/// <inheritdoc/>
		public IDomainContainer DomainContainer { get; }

		/// <inheritdoc/>
		public IQueryProvider NativeProvider => NativeQuery.Provider;

		/// <summary>
		/// The translating provider associated with this query.
		/// </summary>
		public TranslatingQueryProvider TranslatingProvider
		{
			get
			{
				return translatingProvider ??= new EFCoreTranslatingQueryProvider(this.NativeProvider, this.DomainContainer);
			}
		}

		#endregion

		#region Expplicit IEntityQuery implementation

		/// <inheritdoc/>
		IQueryable IEntityQuery.NativeQuery => this.NativeQuery;

		#endregion

		#region Explicit IQueryable implementation

		IQueryProvider IQueryable.Provider => this.TranslatingProvider;

		Type IQueryable.ElementType => NativeQuery.ElementType;

		Expression IQueryable.Expression => NativeQuery.Expression;

		IEnumerator IEnumerable.GetEnumerator()
		{
			var translatedExpression = this.TranslatingProvider.TranslateExpression(NativeQuery.Expression);
			var translatedQuery = this.NativeProvider.CreateQuery(translatedExpression);

			return translatedQuery.GetEnumerator();
		}

		#endregion
	}

	/// <summary>
	/// Implementation of <see cref="IEntityQuery{E}"/> using Entity Framework Core.
	/// </summary>
	/// <typeparam name="E">The type of the entities.</typeparam>
	/// <typeparam name="Q">The type of the Entity Framework Core query object.</typeparam>
	public class EFCoreQuery<E, Q> : EFCoreQuery<Q>, IEntityQuery<E>, IOrderedQueryable<E>
		where Q : IQueryable<E>
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="nativeQuery">The Entity Framework Core query object.</param>
		/// <param name="domainContainer">The domain container which the query pertains to.</param>
		public EFCoreQuery(Q nativeQuery, IDomainContainer domainContainer) : base(nativeQuery, domainContainer)
		{
		}

		#endregion

		#region IEnumerable<E> implementation

		/// <summary>
		/// Executes the query and obtains an enumerator for the results.
		/// </summary>
		public IEnumerator<E> GetEnumerator()
		{
			var translatedExpression = this.TranslatingProvider.TranslateExpression(NativeQuery.Expression);
			var translatedQuery = this.NativeProvider.CreateQuery<E>(translatedExpression);

			return translatedQuery.GetEnumerator();
		}

		#endregion

		#region Public methods

		/// <summary>
		/// The implementation is forwarded to the underlying Entity Framework Core query.
		/// </summary>
		public override bool Equals(object obj)
		{
			var other = obj as EFCoreQuery<E, Q>;

			if (other == null) return false;

			return NativeQuery.Equals(other.NativeQuery);
		}

		/// <summary>
		/// The implementation is forwarded to the underlying Entity Framework Core query.
		/// </summary>
		public override int GetHashCode()
		{
			return NativeQuery.GetHashCode();
		}

		#endregion
	}
}
