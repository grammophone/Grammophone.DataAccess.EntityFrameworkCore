using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Provides information about and control of an entity by implementing <see cref="IEntityEntry{E}"/>.
	/// </summary>
	/// <typeparam name="E">The type of the entity.</typeparam>
	public class EFEntityEntry<E> : IEntityEntry<E>
		where E : class
	{
		#region Private fields

		private readonly EntityEntry<E> underlyingEntityEntry;

		private IReadOnlyDictionary<string, IPropertyEntry<E, object>> propertiesByName;

		#endregion

		#region Construction

		internal EFEntityEntry(EntityEntry<E> underlyingEntry)
		{
			if (underlyingEntry == null) throw new ArgumentNullException(nameof(underlyingEntry));

			this.underlyingEntityEntry = underlyingEntry;
		}

		#endregion

		#region IEntityEntry<E> implementation

		/// <inheritdoc/>
		public E Entity => underlyingEntityEntry.Entity;

		/// <inheritdoc/>
		public TrackingState State
		{
			get => TypeConversions.EntityStateToTrackingState(underlyingEntityEntry.State);
			set => underlyingEntityEntry.State = TypeConversions.TrackingStateToEntityState(value);
		}

		/// <inheritdoc/>
		public IReadOnlyDictionary<string, IPropertyEntry<E, object>> PropertiesByName
			=> propertiesByName ??= CreatePropertiesByName();

		/// <inheritdoc/>
		public void Reload() => underlyingEntityEntry.Reload();

		/// <inheritdoc/>
		public async Task ReloadAsync() => await underlyingEntityEntry.ReloadAsync();

		/// <inheritdoc/>
		public async Task ReloadAsync(CancellationToken cancellationToken) => await underlyingEntityEntry.ReloadAsync(cancellationToken);

		/// <inheritdoc/>
		public IPropertyEntry<E, P> Property<P>(Expression<Func<E, P>> propertySelector)
		{
			if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));

			return new EFPropertyEntry<E, P>(this, underlyingEntityEntry.Property(propertySelector));
		}

		/// <inheritdoc/>
		public IComplexPropertyEntry<E, P> ComplexProperty<P>(Expression<Func<E, P>> propertySelector)
		{
			throw new NotSupportedException("Complex property entries are not implemented for Entity Framework Core yet.");
		}

		/// <inheritdoc/>
		public IReferenceEntry<E, P> Reference<P>(Expression<Func<E, P>> propertySelector) where P : class
		{
			if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));

			return new EFReferenceEntry<E, P>(this, underlyingEntityEntry.Reference(propertySelector));
		}

		/// <inheritdoc/>
		public ICollectionEntry<E, I> Collection<I>(Expression<Func<E, ICollection<I>>> propertySelector) where I : class
		{
			if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));

			return new EFCollectionEntry<E, I>(this, underlyingEntityEntry.Collection<I>(GetMemberName(propertySelector.Body)));
		}

		#endregion

		#region Private methods

		private IReadOnlyDictionary<string, IPropertyEntry<E, object>> CreatePropertiesByName()
		{
			return underlyingEntityEntry.Metadata.GetProperties()
				.Select(p => new EFPropertyEntry<E, object>(this, underlyingEntityEntry.Property<object>(p.Name)))
				.ToDictionary(p => p.Name, p => (IPropertyEntry<E, object>)p);
		}

		private static string GetMemberName(Expression expression)
		{
			if (expression is MemberExpression memberExpression)
			{
				return memberExpression.Member.Name;
			}

			throw new NotSupportedException("Only direct member access expressions are supported.");
		}

		#endregion
	}
}
