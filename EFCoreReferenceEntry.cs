using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core implementation of <see cref="IReferenceEntry{E, P}"/>.
	/// </summary>
	public class EFCoreReferenceEntry<E, P> : IReferenceEntry<E, P>
		where E : class
		where P : class
	{
		#region Private fields

		private readonly ReferenceEntry<E, P> underlyingReferenceEntry;

		#endregion

		#region Construction

		internal EFCoreReferenceEntry(IEntityEntry<E> entityEntry, ReferenceEntry<E, P> underlyingReferenceEntry)
		{
			if (entityEntry == null) throw new ArgumentNullException(nameof(entityEntry));
			if (underlyingReferenceEntry == null) throw new ArgumentNullException(nameof(underlyingReferenceEntry));

			this.EntityEntry = entityEntry;
			this.underlyingReferenceEntry = underlyingReferenceEntry;
		}

		#endregion

		#region IReferenceEntry<E, P> implementation

		/// <inheritdoc/>
		public IEntityEntry<E> EntityEntry { get; }

		/// <inheritdoc/>
		public string Name => underlyingReferenceEntry.Metadata.Name;

		/// <inheritdoc/>
		public P CurrentValue
		{
			get => underlyingReferenceEntry.CurrentValue;
			set => underlyingReferenceEntry.CurrentValue = value;
		}

		/// <inheritdoc/>
		public bool IsLoaded
		{
			get => underlyingReferenceEntry.IsLoaded;
			set => underlyingReferenceEntry.IsLoaded = value;
		}

		/// <inheritdoc/>
		public void Load() => underlyingReferenceEntry.Load();

		/// <inheritdoc/>
		public async Task LoadAsync() => await underlyingReferenceEntry.LoadAsync();

		/// <inheritdoc/>
		public async Task LoadAsync(CancellationToken cancellationToken) => await underlyingReferenceEntry.LoadAsync(cancellationToken);

		/// <inheritdoc/>
		public IQueryable<P> Query() => underlyingReferenceEntry.Query();

		#endregion
	}
}
