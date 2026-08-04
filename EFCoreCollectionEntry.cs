using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core implementation of <see cref="ICollectionEntry{E, I}"/>.
	/// </summary>
	public class EFCoreCollectionEntry<E, I> : ICollectionEntry<E, I>
		where E : class
		where I : class
	{
		#region Private fields

		private readonly CollectionEntry<E, I> underlyingCollectionEntry;

		#endregion

		#region Construction

		internal EFCoreCollectionEntry(IEntityEntry<E> entityEntry, CollectionEntry<E, I> underlyingCollectionEntry)
		{
			if (entityEntry == null) throw new ArgumentNullException(nameof(entityEntry));
			if (underlyingCollectionEntry == null) throw new ArgumentNullException(nameof(underlyingCollectionEntry));

			this.EntityEntry = entityEntry;
			this.underlyingCollectionEntry = underlyingCollectionEntry;
		}

		#endregion

		#region ICollectionEntry<E, I> implementation

		/// <inheritdoc/>
		public IEntityEntry<E> EntityEntry { get; }

		/// <inheritdoc/>
		public string Name => underlyingCollectionEntry.Metadata.Name;

		/// <inheritdoc/>
		public ICollection<I> CurrentValue
		{
			get => (ICollection<I>)underlyingCollectionEntry.CurrentValue;
			set => underlyingCollectionEntry.CurrentValue = value;
		}

		/// <inheritdoc/>
		public bool IsLoaded
		{
			get => underlyingCollectionEntry.IsLoaded;
			set => underlyingCollectionEntry.IsLoaded = value;
		}

		/// <inheritdoc/>
		public void Load() => underlyingCollectionEntry.Load();

		/// <inheritdoc/>
		public async Task LoadAsync() => await underlyingCollectionEntry.LoadAsync();

		/// <inheritdoc/>
		public async Task LoadAsync(CancellationToken cancellationToken) => await underlyingCollectionEntry.LoadAsync(cancellationToken);

		/// <inheritdoc/>
		public IQueryable<I> Query() => underlyingCollectionEntry.Query();

		#endregion
	}
}
