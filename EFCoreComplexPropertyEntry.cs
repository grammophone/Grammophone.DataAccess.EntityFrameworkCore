using System;
using System.Linq.Expressions;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Placeholder Entity Framework Core implementation of <see cref="IComplexPropertyEntry{E, P}"/>.
	/// </summary>
	public class EFCoreComplexPropertyEntry<E, P> : IComplexPropertyEntry<E, P>
		where E : class
	{
		#region Construction

		internal EFCoreComplexPropertyEntry()
		{
		}

		#endregion

		#region IComplexPropertyEntry<E, P> implementation

		/// <inheritdoc/>
		public IEntityEntry<E> EntityEntry => throw new NotSupportedException();

		/// <inheritdoc/>
		public string Name => throw new NotSupportedException();

		/// <inheritdoc/>
		public P CurrentValue { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

		/// <inheritdoc/>
		public P OriginalValue => throw new NotSupportedException();

		/// <inheritdoc/>
		public bool IsModified { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

		/// <inheritdoc/>
		public IPropertyEntry<E, N> Property<N>(Expression<Func<P, N>> subpropertySelector) => throw new NotSupportedException();

		/// <inheritdoc/>
		public IComplexPropertyEntry<E, N> ComplexProperty<N>(Expression<Func<P, N>> subpropertySelector) => throw new NotSupportedException();

		#endregion
	}
}
