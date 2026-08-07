using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Grammophone.DataAccess.EntityFrameworkCore
{
	/// <summary>
	/// Model-building convention which undoes the <see cref="DeleteBehavior.Cascade"/> that Entity
	/// Framework Core applies by convention to required relationships, leaving explicitly configured
	/// delete behavior and many-to-many join keys untouched.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the Entity Framework Core counterpart of Entity Framework 6's
	/// <c>modelBuilder.Conventions.Remove&lt;OneToManyCascadeDeleteConvention&gt;()</c>. With it
	/// registered, a relationship cascades only where the mapping asks for it, which is what Entity
	/// Framework 6 does once that convention is removed.
	/// </para>
	/// <para>
	/// The foreign keys behind many-to-many relationships are deliberately left alone. Entity
	/// Framework 6 removes only <c>OneToManyCascadeDeleteConvention</c> and keeps
	/// <c>ManyToManyCascadeDeleteConvention</c>, because a row of the intermediate table belongs to
	/// neither of the entities it relates and must go when either of them does.
	/// </para>
	/// <para>
	/// Register it from <c>ConfigureConventions</c>:
	/// <code>
	/// configurationBuilder.Conventions.Add(_ => new DeleteConvention());
	/// </code>
	/// </para>
	/// </remarks>
	public sealed class DeleteConvention : IModelFinalizingConvention
	{
		#region Private fields

		private readonly DeleteBehavior deleteBehavior;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="deleteBehavior">
		/// The delete behavior to use in place of the conventional <see cref="DeleteBehavior.Cascade"/>.
		/// </param>
		public DeleteConvention(DeleteBehavior deleteBehavior = DeleteBehavior.NoAction)
		{
			this.deleteBehavior = deleteBehavior;
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Replace the conventional <see cref="DeleteBehavior.Cascade"/> of every relationship which has
		/// not been given an explicit delete behavior and does not implement a many-to-many relationship.
		/// </summary>
		/// <param name="modelBuilder">The builder of the model being finalized.</param>
		/// <param name="context">The context of the convention.</param>
		public void ProcessModelFinalizing(
			IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
		{
			if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

			var manyToManyForeignKeys = GetManyToManyForeignKeys(modelBuilder);

			foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
			{
				foreach (var foreignKey in entityType.GetForeignKeys())
				{
					if (foreignKey.DeleteBehavior != DeleteBehavior.Cascade) continue;

					if (foreignKey.GetDeleteBehaviorConfigurationSource() == ConfigurationSource.Explicit) continue;

					if (manyToManyForeignKeys.Contains(foreignKey)) continue;

					foreignKey.Builder.OnDelete(deleteBehavior);
				}
			}
		}

		#endregion

		#region Private methods

		/// <summary>
		/// The foreign keys which implement the many-to-many relationships of the model, being the ones
		/// carried by the join entity types behind the skip navigations.
		/// </summary>
		/// <param name="modelBuilder">The builder of the model being finalized.</param>
		private static HashSet<IConventionForeignKey> GetManyToManyForeignKeys(IConventionModelBuilder modelBuilder)
		{
			var foreignKeys = modelBuilder.Metadata.GetEntityTypes()
				.SelectMany(entityType => entityType.GetSkipNavigations())
				.Select(skipNavigation => skipNavigation.ForeignKey)
				.Where(foreignKey => foreignKey != null);

			return new HashSet<IConventionForeignKey>(foreignKeys);
		}

		#endregion
	}
}
