using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

#pragma warning disable EF1001 // Internal EF Core API usage.

namespace Grammophone.DataAccess.EntityFrameworkCore.Infrastructure
{
	/// <summary>
	/// Factory that creates a <see cref="ChangeTracker"/> wired to <see cref="HybridChangeDetector"/>
	/// and overrides the model's <c>SkipDetectChanges</c> flag so that
	/// <see cref="ChangeTracker.DetectChanges"/> actually invokes the detector.
	/// </summary>
	public class HybridChangeTrackerFactory : IChangeTrackerFactory
	{
		private readonly DbContext context;
		private readonly IStateManager stateManager;
		private readonly HybridChangeDetector changeDetector;
		private readonly IModel model;
		private readonly IEntityEntryGraphIterator graphIterator;

		/// <summary>
		/// Create.
		/// </summary>
		public HybridChangeTrackerFactory(
			ICurrentDbContext currentContext,
			IStateManager stateManager,
			HybridChangeDetector changeDetector,
			IModel model,
			IEntityEntryGraphIterator graphIterator)
		{
			this.context = currentContext.Context;
			this.stateManager = stateManager;
			this.changeDetector = changeDetector;
			this.model = model;
			this.graphIterator = graphIterator;

			if (this.model is RuntimeModel runtimeModel)
			{
				runtimeModel.SetSkipDetectChanges(false);
			}
		}

		/// <summary>
		/// Creates a new <see cref="ChangeTracker"/> that uses <see cref="HybridChangeDetector"/>
		/// for change detection.
		/// </summary>
		public ChangeTracker Create()
		{
			return new ChangeTracker(this.context, this.stateManager, this.changeDetector, this.model, this.graphIterator);
		}
	}
}

#pragma warning restore EF1001
