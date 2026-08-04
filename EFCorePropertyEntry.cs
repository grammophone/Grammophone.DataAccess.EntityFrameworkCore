using System;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core implementation of <see cref="IPropertyEntry{E, P}"/>.
	/// </summary>
	public class EFCorePropertyEntry<E, P> : IPropertyEntry<E, P>
		where E : class
	{
		#region Private fields

		private readonly PropertyEntry<E, P> underlyingPropertyEntry;

		#endregion

		#region Construction

		internal EFCorePropertyEntry(IEntityEntry<E> entityEntry, PropertyEntry<E, P> underlyingPropertyEntry)
		{
			if (entityEntry == null) throw new ArgumentNullException(nameof(entityEntry));
			if (underlyingPropertyEntry == null) throw new ArgumentNullException(nameof(underlyingPropertyEntry));

			this.EntityEntry = entityEntry;
			this.underlyingPropertyEntry = underlyingPropertyEntry;
		}

		#endregion

		#region IPropertyEntry<E, P> implementation

		/// <inheritdoc/>
		public IEntityEntry<E> EntityEntry { get; }

		/// <inheritdoc/>
		public string Name => underlyingPropertyEntry.Metadata.Name;

		/// <inheritdoc/>
		public P CurrentValue
		{
			get => underlyingPropertyEntry.CurrentValue;
			set => underlyingPropertyEntry.CurrentValue = value;
		}

		/// <inheritdoc/>
		public P OriginalValue => underlyingPropertyEntry.OriginalValue;

		/// <inheritdoc/>
		public bool IsModified
		{
			get => underlyingPropertyEntry.IsModified;
			set => underlyingPropertyEntry.IsModified = value;
		}

		#endregion
	}
}
