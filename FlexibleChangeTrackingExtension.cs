using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable EF1001 // Internal EF Core API usage.

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// <see cref="IDbContextOptionsExtension"/> that registers the hybrid change-detection
	/// services (<see cref="HybridChangeDetector"/>, <see cref="HybridChangeTrackerFactory"/>,
	/// and <see cref="HybridEntityEntrySubscriber"/>).
	/// </summary>
	public class FlexibleChangeTrackingOptionsExtension : IDbContextOptionsExtension
	{
		/// <summary>
		/// Registers the hybrid change-detection services into the context-scoped container.
		/// </summary>
		public void ApplyServices(IServiceCollection services)
		{
			services.AddScoped<HybridChangeDetector>();

			services.AddScoped<IChangeTrackerFactory, HybridChangeTrackerFactory>();

			services.AddScoped<IInternalEntityEntrySubscriber, HybridEntityEntrySubscriber>();
		}

		/// <inheritdoc/>
		public void Validate(IDbContextOptions options) { }

		/// <inheritdoc/>
		public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

		private class ExtensionInfo : DbContextOptionsExtensionInfo
		{
			public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension) { }

			public override bool IsDatabaseProvider => false;
			public override string LogFragment => "flexibleChangeTracking";

			public override int GetServiceProviderHashCode() => 0;

			public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
				=> other is ExtensionInfo;

			public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
		}
	}
}

#pragma warning restore EF1001
