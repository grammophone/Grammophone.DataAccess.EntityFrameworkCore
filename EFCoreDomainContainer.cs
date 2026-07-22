using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Proxies.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// An Entity Framework Core <see cref="DbContext"/> which also implements <see cref="IDomainContainer"/>.
	/// </summary>
	public abstract class EFCoreDomainContainer : DbContext, IDomainContainer
	{
		#region Private classes

		private sealed class EntityListenerMaterializationInterceptor : IMaterializationInterceptor
		{
			public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
			{
				if (materializationData.Context is EFCoreDomainContainer domainContainer)
				{
					foreach (var entityListener in domainContainer.EntityListeners)
					{
						entityListener.OnRead(entity);
					}
				}

				return entity;
			}
		}

		#endregion

		#region Private fields

		private EFCoreChangeTracker changeTracker;

		private IDbContextTransaction dbContextTransaction;

		private static readonly EntityListenerMaterializationInterceptor entityListenerMaterializationInterceptor = new EntityListenerMaterializationInterceptor();

		private static readonly ReferenceSyncInterceptor referenceSyncInterceptor = new ReferenceSyncInterceptor();

		private int transactionNestingLevel;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		protected EFCoreDomainContainer()
			: this(TransactionMode.Real)
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="transactionMode">The transaction behavior.</param>
		protected EFCoreDomainContainer(TransactionMode transactionMode)
		{
			this.TransactionMode = transactionMode;
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="options">The context options.</param>
		protected EFCoreDomainContainer(DbContextOptions options)
			: this(DecorateOptions(options), TransactionMode.Real)
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="options">The context options.</param>
		/// <param name="transactionMode">The transaction behavior.</param>
		protected EFCoreDomainContainer(DbContextOptions options, TransactionMode transactionMode)
			: base(DecorateOptions(options))
		{
			this.TransactionMode = transactionMode;
		}

		/// <summary>
		/// This method is not used. Service replacements happen solely via <c>OnConfiguring</c>.
		/// Early registration in constructors does not affect <c>ChangeTracker</c>'s cached <c>IChangeDetector</c>.
		/// </summary>
		private static DbContextOptions DecorateOptions(DbContextOptions options) => options;

		#endregion

		#region IDomainContainer implementation

		/// <inheritdoc/>
		IChangeTracker IDomainContainer.ChangeTracker => changeTracker ??= new EFCoreChangeTracker(this);

		/// <inheritdoc/>
		public ICollection<IEntityListener> EntityListeners { get; } = new List<IEntityListener>();

		/// <inheritdoc/>
		public bool IsProxyCreationEnabled
		{
			get
			{
#pragma warning disable EF1001 // Internal EF Core API usage.
				return this.GetService<IDbContextServices>()?.ContextOptions.FindExtension<ProxiesOptionsExtension>()?.UseProxies ?? false;
#pragma warning restore EF1001 // Internal EF Core API usage.
			}
			set
			{
				throw new DataAccessException("Proxy class creation setting is only available during the domain container setup.");
			}
		}

		/// <inheritdoc/>
		public bool IsLazyLoadingEnabled
		{
			get => this.ChangeTracker.LazyLoadingEnabled;
			set => this.ChangeTracker.LazyLoadingEnabled = value;
		}

		/// <inheritdoc/>
		public TransactionMode TransactionMode { get; private set; }

		/// <summary>
		/// If true, EF Core change-tracking proxies are enabled in addition to lazy-loading proxies.
		/// </summary>
		/// <remarks>
		/// When enabled, EF Core requires all mapped properties of entity classes to be virtual,
		/// including scalar properties, key properties, reference navigations and collection navigations.
		/// Collection navigation implementations must support change notifications, for example by using
		/// <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/>.
		/// </remarks>
		public bool UseChangeTracking { get; }

		/// <summary>
		/// Optional <see cref="IExceptionTransformer"/> to be used during saving changes
		/// and <see cref="TranslateException(SystemException)"/> methods.
		/// Default value is null.
		/// </summary>
		public IExceptionTransformer ExceptionTransformer { get; set; }

		/// <inheritdoc/>
		object IContextOwner.UnderlyingContext => this;

		/// <inheritdoc/>
		IEntityEntry<E> IDomainContainer.Entry<E>(E entity) => GetEntry(entity);

		/// <inheritdoc/>
		public override int SaveChanges()
		{
			if (TransactionMode == TransactionMode.Deferred && transactionNestingLevel >= 1) return 0;

			var addedEntries = DetectAndNotifySavingChanges();

			try
			{
				int changesCount = base.SaveChanges();

				NotifyAddedEntriesSaved(addedEntries);

				return changesCount;
			}
			catch (DbUpdateException updateException)
			{
				throw TranslateUpdateException(updateException);
			}
		}

		/// <inheritdoc/>
		public Task<int> SaveChangesAsync()
		{
			return SaveChangesAsync(default(CancellationToken));
		}

		/// <inheritdoc/>
		public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		{
			if (TransactionMode == TransactionMode.Deferred && transactionNestingLevel >= 1) return 0;

			var addedEntries = DetectAndNotifySavingChanges();

			try
			{
				int changesCount = await base.SaveChangesAsync(cancellationToken);

				NotifyAddedEntriesSaved(addedEntries);

				return changesCount;
			}
			catch (DbUpdateException updateException)
			{
				throw TranslateUpdateException(updateException);
			}
		}

		/// <inheritdoc/>
		public void SetAsModified(object entity)
		{
			Entry(entity).State = EntityState.Modified;
		}

		/// <inheritdoc/>
		public void AttachGraphAsModified<T>(T graphRoot) where T : class
		{
			ChangeTracker.TrackGraph(graphRoot, node => node.Entry.State = EntityState.Modified);
		}

		/// <inheritdoc/>
		public new void Attach<E>(E entity) where E : class
		{
			Set<E>().Attach(entity);
		}

		/// <inheritdoc/>
		public void Detach(object entity)
		{
			Entry(entity).State = EntityState.Detached;
		}

		/// <inheritdoc/>
		public ITransaction BeginTransaction()
		{
			transactionNestingLevel++;

			if (TransactionMode == TransactionMode.Real && transactionNestingLevel == 1)
			{
				dbContextTransaction = Database.BeginTransaction();
			}

			return new EFCoreTransaction(this);
		}

		/// <inheritdoc/>
		public ITransaction BeginTransaction(IsolationLevel isolationLevel)
		{
			transactionNestingLevel++;

			if (TransactionMode == TransactionMode.Real && transactionNestingLevel == 1)
			{
				dbContextTransaction = Database.BeginTransaction(isolationLevel);
			}

			return new EFCoreTransaction(this);
		}

		/// <inheritdoc/>
		public T Create<T>() where T : class
		{
			if (this.IsProxyCreationEnabled)
			{
				var proxy = this.CreateProxy<T>();
				
				TouchProxyNavigationGetters(proxy);
				
				return proxy;
			}

			return Activator.CreateInstance<T>();
		}

		private static void TouchProxyNavigationGetters<TEntity>(TEntity proxy)
			where TEntity : class
		{
			var proxyType = proxy.GetType();
			var baseType = proxyType.BaseType;

			if (baseType == null || proxyType == baseType) return;

			foreach (var property in proxyType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
			{
				// Only touch properties inherited from the entity class, not proxy-injected properties.
				if (property.CanRead && baseType.GetProperty(property.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public) != null)
				{
					try { property.GetValue(proxy); }
					catch { /* Swallow — property access is just for proxy initialization */ }
				}
			}
		}

		/// <inheritdoc/>
		public virtual Exception TranslateException(SystemException exception)
		{
			switch (exception)
			{
				case DbException dbException:
					return TranslateDbException(dbException);

				default:
					return exception;
			}
		}

		/// <inheritdoc/>
		public virtual QueryTranslator TryGetQueryTranslator()
		{
			return EFCoreQueryTranslatorFactory.GetQueryTranslator();
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Gets an entity entry for an entity.
		/// </summary>
		/// <typeparam name="E">The entity type.</typeparam>
		/// <param name="entity">The entity.</param>
		/// <returns>Returns the entity entry.</returns>
		public IEntityEntry<E> GetEntry<E>(E entity) where E : class
		{
			return new EFCoreEntityEntry<E>(Entry(entity));
		}

		#endregion

		#region Protected methods

		/// <summary>
		/// Enables lazy-loading proxies and, when requested, change-tracking proxies.
		/// </summary>
		/// <param name="optionsBuilder">The builder used to create or modify options for this context.</param>
		/// <remarks>
		/// Derived domain containers overriding this method must call the base implementation if
		/// <see cref="IDomainContainer.Create{T}"/> is expected to create proxy instances.
		/// If <see cref="UseChangeTracking"/> is true, EF Core also requires all mapped entity properties to be virtual
		/// and collection navigations to support change notifications.
		/// </remarks>
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			optionsBuilder.AddInterceptors(referenceSyncInterceptor, entityListenerMaterializationInterceptor);

			optionsBuilder.ReplaceService<IQueryTranslationPreprocessorFactory, QueryTamingPreprocessorFactory>();

			optionsBuilder.UseLazyLoadingProxies();
		}

		/// <summary>
		/// Set the delete behavior for relationships conventionally configured for database cascade delete.
		/// </summary>
		/// <param name="modelBuilder">The model builder being configured.</param>
		/// <param name="deleteBehavior">The delete behavior to use instead of database cascade delete.</param>
		/// <remarks>
		/// Call this near the end of <c>OnModelCreating</c> to keep convention-inferred
		/// required relationships while replacing their default <see cref="DeleteBehavior.Cascade"/> behavior.
		/// Configure explicit exceptions after calling this method.
		/// </remarks>
		protected void SetDefaultDeleteBehavior(ModelBuilder modelBuilder, DeleteBehavior deleteBehavior = DeleteBehavior.ClientCascade)
		{
			if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

			foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetForeignKeys()))
			{
				if (foreignKey.DeleteBehavior == DeleteBehavior.Cascade)
				{
					foreignKey.DeleteBehavior = deleteBehavior;
				}
			}
		}

		#endregion

		#region Internal methods

		internal void OnCommitTransaction()
		{
			SaveChanges();

			if (TransactionMode == TransactionMode.Real && transactionNestingLevel == 1)
			{
				dbContextTransaction?.Commit();
			}
		}

		internal async Task OnCommitTransactionAsync(CancellationToken cancellationToken)
		{
			await SaveChangesAsync(cancellationToken);

			if (TransactionMode == TransactionMode.Real && transactionNestingLevel == 1 && dbContextTransaction != null)
			{
				await dbContextTransaction.CommitAsync(cancellationToken);
			}
		}

		internal void DisposeTransaction(bool passed)
		{
			transactionNestingLevel--;

			if (transactionNestingLevel == 0)
			{
				if (!passed)
				{
					dbContextTransaction?.Rollback();
				}

				dbContextTransaction?.Dispose();
				dbContextTransaction = null;
			}
		}

		#endregion

		#region Private methods

		private DataAccessException TranslateUpdateException(DbUpdateException updateException)
		{
			var dbException = FindDbException(updateException);

			if (dbException != null)
			{
				return TranslateDbException(dbException);
			}

			return new DataAccessException(
				updateException.Message,
				updateException.InnerException ?? updateException);
		}

		private DataAccessException TranslateDbException(DbException dbException)
		{
			if (this.ExceptionTransformer != null)
			{
				return this.ExceptionTransformer.TranslateDbException(dbException);
			}

			return new DataAccessException(dbException.Message, dbException);
		}

		private static DbException FindDbException(Exception exception)
		{
			while (exception != null)
			{
				if (exception is DbException dbException)
				{
					return dbException;
				}

				exception = exception.InnerException;
			}

			return null;
		}

		private IReadOnlyList<EntityEntry> DetectAndNotifySavingChanges()
		{
			this.ChangeTracker.DetectChanges();

			if (this.EntityListeners.Count == 0) return Array.Empty<EntityEntry>();

			var deletedEntries = ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).ToArray();
			var changedEntries = ChangeTracker.Entries().Where(e => e.State == EntityState.Modified).ToArray();
			var addedEntries = ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToArray();

			foreach (var entityListener in this.EntityListeners)
			{
				foreach (var deletedEntry in deletedEntries)
				{
					entityListener.OnDeleting(deletedEntry.Entity);
				}

				foreach (var changedEntry in changedEntries)
				{
					entityListener.OnChanging(changedEntry.Entity);
				}

				foreach (var addedEntry in addedEntries)
				{
					entityListener.OnAdding(addedEntry.Entity);
				}
			}

			this.ChangeTracker.DetectChanges();

			return addedEntries;
		}

		private void NotifyAddedEntriesSaved(IReadOnlyList<EntityEntry> addedEntries)
		{
			foreach (var addedEntry in addedEntries)
			{
				if (addedEntry.State != EntityState.Unchanged) continue;

				foreach (var entityListener in this.EntityListeners)
				{
					entityListener.OnAdded(addedEntry.Entity);
				}
			}
		}

		#endregion
	}
}
