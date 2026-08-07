using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Proxies.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

using Grammophone.DataAccess.EntityFrameworkCore.Infrastructure;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// An Entity Framework Core <see cref="DbContext"/> which also implements <see cref="IDomainContainer"/>.
	/// </summary>
	public abstract class EFCoreDomainContainer : DbContext, IDomainContainer
	{
		#region Private classes

		/// <summary>
		/// Notifies <see cref="IEntityListener.OnRead(object)"/> for the materializations which
		/// <see cref="ChangeTracker.Tracked"/> will not report.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Read notification cannot be issued from here for a tracking query. Entity Framework Core
		/// materializes the instance, runs the materialization interceptors, and only then calls
		/// <c>StartTrackingFromQuery</c> — which begins by looking the instance up and returns early when
		/// it finds an entry, reaching the state assignment which actually tracks the entity only when it
		/// has to create that entry itself. <see cref="DbContext.Entry(object)"/> is not an inspection: it
		/// resolves to <c>GetOrCreateEntry</c>, which creates a <c>Detached</c> entry and registers it. So
		/// a listener calling <c>Entry</c> in this window — as the lazy loader does on the first
		/// dereference of any navigation, which is how access checking reaches a tenant held behind one —
		/// takes the slot tracking was about to fill, and the query yields an untracked entity while the
		/// change tracker reports no entry at all.
		/// </para>
		/// <para>
		/// Notification for tracking queries therefore happens in <see cref="OnEntityTracked"/>, after the
		/// entity is tracked and the window is closed. This interceptor keeps the cases that event cannot
		/// report: materializations outside a query, no-tracking queries — neither of which is followed by
		/// any tracking, so there is no slot to take — and instances which are already tracked, for which
		/// <c>StartTrackingFromQuery</c> returns early and raises nothing. The two paths are mutually
		/// exclusive by construction: the condition below is exactly "<see cref="ChangeTracker.Tracked"/>
		/// will not fire for this instance", so every materialized entity is notified exactly once.
		/// </para>
		/// </remarks>
		private sealed class UntrackedReadNotificationInterceptor : IMaterializationInterceptor
		{
			public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
			{
				if (!(materializationData.Context is EFCoreDomainContainer domainContainer)) return entity;

				// Subscribed from here rather than from a constructor because reading ChangeTracker forces
				// the context's services — and therefore OnConfiguring — to initialize, which must not
				// happen before a derived container's constructor has finished. Materialization always
				// precedes tracking, so the subscription is established before it can be needed.
				domainContainer.EnsureReadNotificationSubscription();

				if (materializationData.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll
					&& !domainContainer.IsAlreadyTracked(materializationData.EntityType, entity))
				{
					return entity;
				}

				domainContainer.NotifyRead(entity);

				return entity;
			}
		}

		#endregion

		#region Private fields

		private EFCoreChangeTracker changeTracker;

		private IDbContextTransaction dbContextTransaction;

		private static readonly UntrackedReadNotificationInterceptor untrackedReadNotificationInterceptor = new UntrackedReadNotificationInterceptor();

		private static readonly ReferenceSyncInterceptor referenceSyncInterceptor = new ReferenceSyncInterceptor();

		private bool isReadNotificationSubscribed;

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

		/// <remarks>
		/// Collection properties are left untouched. Reading one on a proxy which is still detached makes its
		/// lazy loader record the collection as loaded while empty, and that verdict survives the entity being
		/// tracked: the collection then stays empty for the rest of the instance's life, even once the database
		/// holds rows for it. That is what happens to an entity whose collection is filled by a set-based update
		/// rather than through the entity itself, which cannot reach the tracked instance to begin with.
		/// </remarks>
		private static void TouchProxyNavigationGetters<TEntity>(TEntity proxy)
			where TEntity : class
		{
			var proxyType = proxy.GetType();
			var baseType = proxyType.BaseType;

			if (baseType == null || proxyType == baseType) return;

			foreach (var property in proxyType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
			{
				if (IsCollectionProperty(property)) continue;

				// Only touch properties inherited from the entity class, not proxy-injected properties.
				if (property.CanRead && baseType.GetProperty(property.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public) != null)
				{
					try { property.GetValue(proxy); }
					catch { /* Swallow — property access is just for proxy initialization */ }
				}
			}
		}

		/// <summary>
		/// Determines whether a property holds a collection, strings excluded.
		/// </summary>
		private static bool IsCollectionProperty(System.Reflection.PropertyInfo property)
		{
			var propertyType = property.PropertyType;

			if (propertyType == typeof(string) || propertyType.IsArray) return false;

			return typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType);
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
			// Order matters: reference synchronization runs first, so the read-notification interceptor
			// tests the instance which is actually going to be tracked.
			optionsBuilder.AddInterceptors(referenceSyncInterceptor, untrackedReadNotificationInterceptor);

			optionsBuilder.ReplaceService<IQueryTranslationPreprocessorFactory, QueryTamingPreprocessorFactory>();

			optionsBuilder.UseLazyLoadingProxies();
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

#pragma warning disable EF1001 // Suppress Internal EF Core API warning

		/// <summary>
		/// Subscribes <see cref="OnEntityTracked"/>, once per container.
		/// </summary>
		private void EnsureReadNotificationSubscription()
		{
			if (isReadNotificationSubscribed) return;

			isReadNotificationSubscribed = true;

			this.ChangeTracker.Tracked += OnEntityTracked;
		}

		/// <summary>
		/// Issues read notification for entities arriving from a tracking query, once they are tracked.
		/// </summary>
		/// <remarks>
		/// Raised synchronously while the row is being shaped, so a listener rejecting the entity still
		/// aborts the query before the caller can observe it. Entities tracked by being added or attached
		/// are not reads and are reported through <see cref="IEntityListener.OnAdding(object)"/> and
		/// <see cref="IEntityListener.OnAdded(object)"/> instead.
		/// </remarks>
		private void OnEntityTracked(object sender, EntityTrackedEventArgs args)
		{
			if (!args.FromQuery) return;

			NotifyRead(args.Entry.Entity);
		}

		/// <summary>
		/// Notifies every listener that an entity has been read.
		/// </summary>
		/// <param name="entity">The entity which has been read.</param>
		private void NotifyRead(object entity)
		{
			foreach (var entityListener in this.EntityListeners)
			{
				entityListener.OnRead(entity);
			}
		}

		/// <summary>
		/// Determines whether this very instance is already tracked, in which case
		/// <c>StartTrackingFromQuery</c> returns early and <see cref="ChangeTracker.Tracked"/> is not
		/// raised for it.
		/// </summary>
		/// <param name="entityType">The entity type being materialized.</param>
		/// <param name="entity">The materialized instance.</param>
		/// <remarks>
		/// A lookup only. Deliberately not <see cref="DbContext.Entry(object)"/>, which would create the
		/// very entry whose absence is being tested for.
		/// </remarks>
		private bool IsAlreadyTracked(Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType, object entity)
		{
			var primaryKey = entityType.FindPrimaryKey();

			if (primaryKey == null) return false;

			var keyValues = primaryKey.Properties.Select(p => p.GetGetter().GetClrValue(entity)).ToArray();

			var internalEntry = this.GetService<IStateManager>().TryGetEntry(primaryKey, keyValues);

			return internalEntry != null && ReferenceEquals(internalEntry.Entity, entity);
		}

#pragma warning restore EF1001

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

			// Undo the premature "loaded" state that TouchProxyNavigationGetters leaves on reference
			// navigations of freshly created proxies. Touching a reference navigation getter on a detached
			// new proxy is required so that a value assigned to the navigation itself survives, but it also
			// marks the navigation as loaded with a null value. When a foreign key is assigned afterwards
			// (e.g. Detail.TypeID = 517) instead of the navigation, the navigation would stay null forever.
			// Here, right before saving, we clear that state for still-null dependent references, so they
			// lazy-load from their assigned foreign key once the entity is tracked.
			ResetStuckDependentReferences();

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

		/// <summary>
		/// Clears the <c>IsLoaded</c> flag on dependent reference navigations that are still null but whose
		/// foreign key has been assigned a value, so they lazy-load correctly once the entity is tracked.
		/// This counteracts the premature "loaded" state that <see cref="TouchProxyNavigationGetters{TEntity}"/>
		/// leaves on freshly created proxies.
		/// </summary>
		private void ResetStuckDependentReferences()
		{
			foreach (var entry in this.ChangeTracker.Entries())
			{
				if (entry.State != EntityState.Added && entry.State != EntityState.Modified) continue;

				foreach (var reference in entry.References)
				{
					if (!reference.IsLoaded || reference.CurrentValue != null) continue;

					// Only navigations whose foreign key lives on this entity can be lazy-loaded from it.
					if (!(reference.Metadata is Microsoft.EntityFrameworkCore.Metadata.INavigation navigation) || !navigation.IsOnDependent) continue;

					var foreignKeyHasValue = false;

					foreach (var foreignKeyProperty in navigation.ForeignKey.Properties)
					{
						var value = entry.Property(foreignKeyProperty.Name).CurrentValue;

						if (!IsDefaultValue(value)) { foreignKeyHasValue = true; break; }
					}

					if (foreignKeyHasValue) reference.IsLoaded = false;
				}
			}
		}

		/// <summary>
		/// Determines whether a value equals the default of its type (null, or the zero value for value types).
		/// </summary>
		private static bool IsDefaultValue(object value)
		{
			if (value == null) return true;

			var type = value.GetType();

			if (!type.IsValueType) return false;

			return value.Equals(Activator.CreateInstance(type));
		}

		#endregion
	}
}
