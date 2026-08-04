using System;
using System.Linq;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core implementation of the translating query provider.
	/// </summary>
	public class EFCoreTranslatingQueryProvider : TranslatingQueryProvider
	{
		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="nativeQueryProvider">The underlying Entity Framework Core query provider.</param>
		/// <param name="domainContainer">The domain container which owns the query.</param>
		public EFCoreTranslatingQueryProvider(IQueryProvider nativeQueryProvider, IDomainContainer domainContainer) : base(nativeQueryProvider, domainContainer)
		{
		}

		#endregion

		#region Protected methods

		/// <inheritdoc/>
		protected override IEntityQuery WrapNativeQuery(IQueryable nativeQueryable)
		{
			if (nativeQueryable == null) throw new ArgumentNullException(nameof(nativeQueryable));

			return new EFCoreQuery<IQueryable>(nativeQueryable, this.DomainContainer);
		}

		/// <inheritdoc/>
		protected override IEntityQuery<T> WrapNativeQuery<T>(IQueryable<T> nativeQueryable)
		{
			if (nativeQueryable == null) throw new ArgumentNullException(nameof(nativeQueryable));

			return new EFCoreQuery<T, IQueryable<T>>(nativeQueryable, this.DomainContainer);
		}

		#endregion
	}
}
