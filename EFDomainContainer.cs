using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// An Entity Framework Core <see cref="DbContext"/> which also implements <see cref="IDomainContainer"/>.
	/// </summary>
	public abstract class EFDomainContainer : DbContext, IDomainContainer
	{
		#region Private fields

		private EFChangeTracker changeTracker;

		private IDbContextTransaction dbContextTransaction;

		private int transactionNestingLevel;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		protected EFDomainContainer()
			: this(TransactionMode.Real)
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="transactionMode">The transaction behavior.</param>
		protected EFDomainContainer(TransactionMode transactionMode)
		{
			this.TransactionMode = transactionMode;
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="options">The context options.</param>
		protected EFDomainContainer(DbContextOptions options)
			: this(options, TransactionMode.Real)
		{
		}

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="options">The context options.</param>
		/// <param name="transactionMode">The transaction behavior.</param>
		protected EFDomainContainer(DbContextOptions options, TransactionMode transactionMode)
			: base(options)
		{
			this.TransactionMode = transactionMode;
		}

		#endregion

		#region IDomainContainer implementation

		/// <inheritdoc/>
		IChangeTracker IDomainContainer.ChangeTracker => changeTracker ??= new EFChangeTracker(this);

		/// <inheritdoc/>
		public ICollection<IEntityListener> EntityListeners { get; } = new List<IEntityListener>();

		/// <inheritdoc/>
		public bool IsProxyCreationEnabled { get; set; } = true;

		/// <inheritdoc/>
		public bool IsLazyLoadingEnabled
		{
			get => this.ChangeTracker.LazyLoadingEnabled;
			set => this.ChangeTracker.LazyLoadingEnabled = value;
		}

		/// <inheritdoc/>
		public TransactionMode TransactionMode { get; private set; }

		/// <inheritdoc/>
		object IContextOwner.UnderlyingContext => this;

		/// <inheritdoc/>
		IEntityEntry<E> IDomainContainer.Entry<E>(E entity) => GetEntry(entity);

		/// <inheritdoc/>
		public override int SaveChanges()
		{
			if (TransactionMode == TransactionMode.Deferred && transactionNestingLevel >= 1) return 0;

			return base.SaveChanges();
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

			return await base.SaveChangesAsync(cancellationToken);
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

			return new EFTransaction(this);
		}

		/// <inheritdoc/>
		public ITransaction BeginTransaction(IsolationLevel isolationLevel)
		{
			transactionNestingLevel++;

			if (TransactionMode == TransactionMode.Real && transactionNestingLevel == 1)
			{
				dbContextTransaction = Database.BeginTransaction(isolationLevel);
			}

			return new EFTransaction(this);
		}

		/// <inheritdoc/>
		public T Create<T>() where T : class
		{
			return Activator.CreateInstance<T>();
		}

		/// <inheritdoc/>
		public virtual Exception TranslateException(SystemException exception)
		{
			return exception;
		}

		/// <inheritdoc/>
		public virtual QueryTranslator TryGetQueryTranslator()
		{
			return EFQueryTranslatorFactory.GetQueryTranslator();
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
			return new EFEntityEntry<E>(Entry(entity));
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
	}
}
