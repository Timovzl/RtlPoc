using Microsoft.Azure.Cosmos;
using Rtl.News.RtlPoc.Application.Promises;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;

public class MigrationListProvider
{
	private static FrozenDictionary<string, Action<ContainerProperties>> Migrations { get; } = new MigrationListBuilder()

		// DO NOT MODIFY OR DELETE, BUT APPEND TO END ONLY

		// Start with an empty migration
		.CreateInitial("Initial migration")

		// Save on index usage for promises, which are common and short-lived and should have minimal overhead
		.ExcludeIndex("Exclude Promise.AttemptCount", (Promise promise) => promise.AttemptCount)
		.ExcludeIndex("Exclude Promise.ActionName", (Promise promise) => promise.ActionName)
		.ExcludeIndex("Exclude Promise.Data", (Promise promise) => promise.Data)

		// Append migrations above this line, such as new composite indexes...

		.Build();

	/// <summary>
	/// Returns the complete set of migrations by name.
	/// </summary>
	public virtual FrozenDictionary<string, Action<ContainerProperties>> GetMigrations()
	{
		return Migrations;
	}

	/// <summary>
	/// Applies <em>all</em> migrations to the given <paramref name="containerProperties"/>.
	/// </summary>
	public void ApplyAllMigrations(ContainerProperties containerProperties)
	{
		foreach (var (_, mutation) in this.GetMigrations())
			mutation(containerProperties);
	}

	private sealed class MigrationListBuilder()
	{
		private readonly Dictionary<string, Action<ContainerProperties>> _mutationAccumulator = [];

		private void AddDelta(string description, Action<ContainerProperties> mutation)
		{
			// Add the mutation
			this._mutationAccumulator.Add(description, mutation);
		}

		public MigrationListBuilder CreateInitial(string description)
		{
			this._mutationAccumulator.Add(description, _ => { });
			return this;
		}

		public MigrationListBuilder AddCompositeIndex<TEntity, TProperty>(
			string description,
			Expression<Func<TEntity, TProperty>>[] properties,
			CompositePathSortOrder sortOrder = CompositePathSortOrder.Ascending)
		{
			// Define the index
			var index = properties
				.Select(property => new CompositePath()
				{
					Order = sortOrder,
					Path = JsonUtilities.GetPropertyPath(property),
				})
				.ToList();

			this.AddDelta(
				description,
				containerProperties => containerProperties.IndexingPolicy.CompositeIndexes.Add(new Collection<CompositePath>(index)));

			return this;
		}

		public MigrationListBuilder ExcludeIndex<TEntity, TProperty>(
			string description,
			Expression<Func<TEntity, TProperty>> property)
		{
			var excludedPath = new ExcludedPath() { Path = $"{JsonUtilities.GetPropertyPath(property)}/?" };

			this.AddDelta(
				description,
				containerProperties => containerProperties.IndexingPolicy.ExcludedPaths.Add(excludedPath));

			return this;
		}

		public FrozenDictionary<string, Action<ContainerProperties>> Build()
		{
			return this._mutationAccumulator.ToFrozenDictionary();
		}
	}
}
