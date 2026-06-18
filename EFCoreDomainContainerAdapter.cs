using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Use this domain container implementation if you plan to expose entity sets as <see cref="IEntitySet{E}"/>.
	/// </summary>
	/// <typeparam name="D">The adapted Entity Framework Core domain container type.</typeparam>
	public abstract class EFCoreDomainContainerAdapter<D> : IDomainContainer
		where D : EFCoreDomainContainer
	{
		#region Protected properties

		/// <summary>
		/// The adapted <see cref="EFCoreDomainContainer"/>.
		/// </summary>
		protected D InnerDomainContainer { get; }

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="innerContainer">The adapted domain container.</param>
		public EFCoreDomainContainerAdapter(D innerContainer)
		{
			if (innerContainer == null) throw new ArgumentNullException(nameof(innerContainer));

			this.InnerDomainContainer = innerContainer;
		}

		#endregion

		#region IDomainContainer implementation

		/// <inheritdoc/>
		public IChangeTracker ChangeTracker => ((IDomainContainer)this.InnerDomainContainer).ChangeTracker;

		/// <inheritdoc/>
		public ICollection<IEntityListener> EntityListeners => this.InnerDomainContainer.EntityListeners;

		/// <inheritdoc/>
		public bool IsProxyCreationEnabled
		{
			get => InnerDomainContainer.IsProxyCreationEnabled;
			set => InnerDomainContainer.IsProxyCreationEnabled = value;
		}

		/// <inheritdoc/>
		public bool IsLazyLoadingEnabled
		{
			get => InnerDomainContainer.IsLazyLoadingEnabled;
			set => InnerDomainContainer.IsLazyLoadingEnabled = value;
		}

		/// <inheritdoc/>
		public TransactionMode TransactionMode => this.InnerDomainContainer.TransactionMode;

		/// <inheritdoc/>
		object IContextOwner.UnderlyingContext => ((IContextOwner)this.InnerDomainContainer).UnderlyingContext;

		/// <inheritdoc/>
		public IEntityEntry<E> Entry<E>(E entity) where E : class => ((IDomainContainer)this.InnerDomainContainer).Entry(entity);

		/// <inheritdoc/>
		public int SaveChanges() => this.InnerDomainContainer.SaveChanges();

		/// <inheritdoc/>
		public async Task<int> SaveChangesAsync() => await this.InnerDomainContainer.SaveChangesAsync();

		/// <inheritdoc/>
		public async Task<int> SaveChangesAsync(CancellationToken cancellationToken) => await this.InnerDomainContainer.SaveChangesAsync(cancellationToken);

		/// <inheritdoc/>
		public void SetAsModified(object entity) => this.InnerDomainContainer.SetAsModified(entity);

		/// <inheritdoc/>
		public void AttachGraphAsModified<T>(T graphRoot) where T : class => this.InnerDomainContainer.AttachGraphAsModified(graphRoot);

		/// <inheritdoc/>
		public void Attach<E>(E entity) where E : class => this.InnerDomainContainer.Attach(entity);

		/// <inheritdoc/>
		public void Detach(object entity) => this.InnerDomainContainer.Detach(entity);

		/// <inheritdoc/>
		public ITransaction BeginTransaction() => this.InnerDomainContainer.BeginTransaction();

		/// <inheritdoc/>
		public ITransaction BeginTransaction(IsolationLevel isolationLevel) => this.InnerDomainContainer.BeginTransaction(isolationLevel);

		/// <inheritdoc/>
		public T Create<T>() where T : class => this.InnerDomainContainer.Create<T>();

		/// <inheritdoc/>
		public Exception TranslateException(SystemException exception) => this.InnerDomainContainer.TranslateException(exception);

		/// <inheritdoc/>
		public QueryTranslator TryGetQueryTranslator() => this.InnerDomainContainer.TryGetQueryTranslator();

		/// <inheritdoc/>
		public void Dispose() => this.InnerDomainContainer.Dispose();

		#endregion
	}
}
