using System;
using System.Threading;
using System.Threading.Tasks;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Entity Framework Core implementation of <see cref="ITransaction"/>.
	/// </summary>
	public class EFCoreTransaction : ITransaction
	{
		#region Private fields

		private readonly EFCoreDomainContainer domainContainer;

		private bool isPassed;

		private bool isDisposed;

		#endregion

		#region Construction

		internal EFCoreTransaction(EFCoreDomainContainer domainContainer)
		{
			if (domainContainer == null) throw new ArgumentNullException(nameof(domainContainer));

			this.domainContainer = domainContainer;
		}

		#endregion

		#region ITransaction implementation

		/// <inheritdoc/>
		public event Action Succeeding;

		/// <inheritdoc/>
		public event Action RollingBack;

		/// <inheritdoc/>
		public void Commit()
		{
			domainContainer.OnCommitTransaction();
			isPassed = true;
		}

		/// <inheritdoc/>
		public async Task CommitAsync()
		{
			await CommitAsync(default(CancellationToken));
		}

		/// <inheritdoc/>
		public async Task CommitAsync(CancellationToken cancellationToken)
		{
			await domainContainer.OnCommitTransactionAsync(cancellationToken);
			isPassed = true;
		}

		/// <inheritdoc/>
		public void Pass()
		{
			isPassed = true;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			if (isDisposed) return;

			isDisposed = true;

			if (isPassed)
				Succeeding?.Invoke();
			else
				RollingBack?.Invoke();

			domainContainer.DisposeTransaction(isPassed);
		}

		#endregion
	}
}
