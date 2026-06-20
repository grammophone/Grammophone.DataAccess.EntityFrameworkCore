# Entity Framework Core Setup

This project adapts EF Core 8 to the `Grammophone.DataAccess` contracts.

## Domain Container

Define a domain container by deriving from `EFCoreDomainContainer`:

```csharp
public class EFCoreMusicDomainContainer : EFCoreDomainContainer
{
	public EFCoreMusicDomainContainer(DbContextOptions options)
		: base(options, useChangeTracking: true)
	{
	}

	public DbSet<Artist> Artists { get; set; }
	public DbSet<Album> Albums { get; set; }
	public DbSet<Track> Tracks { get; set; }
	public DbSet<Genre> Genres { get; set; }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Artist>().HasKey(a => a.ID);
		modelBuilder.Entity<Artist>().Property(a => a.Name).IsRequired().HasMaxLength(200);
		modelBuilder.Entity<Artist>().HasIndex(a => a.Name).IsUnique();
	}
}
```

`EFCoreDomainContainer.OnConfiguring` always enables lazy-loading proxies. Change-tracking proxies are controlled by the required `useChangeTracking` constructor argument. Derived contexts overriding `OnConfiguring` must call `base.OnConfiguring(optionsBuilder)` if `IDomainContainer.Create<T>()` is expected to create proxy instances.

Application code should use `IDomainContainer.Create<T>()` or `IEntitySet<T>.Create()` for new entities, not provider-specific factory APIs.

The example specifies `useChangeTracking: true` deliberately because the sample music entities are designed for change-tracking proxies: every mapped property is virtual and collection navigations use notification-capable collections. In ordinary migrations from EF6, choose this value consciously. Use `false` to keep EF Core snapshot tracking while still enabling lazy-loading proxies.

## Options

Configure EF Core normally:

```csharp
var options = new DbContextOptionsBuilder<EFCoreMusicDomainContainer>()
	.UseSqlServer(connectionString)
	.Options;
```

The base container configures lazy-loading proxy support. If `useChangeTracking` is `true`, entity classes must also satisfy EF Core change-tracking proxy requirements: every mapped property must be `virtual` without exception, including scalar properties, key properties, reference navigations and collection navigations. Collection navigation implementations must support change notifications, for example by using `ObservableCollection<T>`.

## Adapter

Expose a provider-neutral contract through an adapter:

```csharp
public class EFCoreMusicDomainContainerAdapter :
	EFCoreDomainContainerAdapter<EFCoreMusicDomainContainer>,
	IMusicDomainContainer
{
	private IEntitySet<Album> albums;

	public EFCoreMusicDomainContainerAdapter(EFCoreMusicDomainContainer innerContainer)
		: base(innerContainer)
	{
	}

	public IEntitySet<Album> Albums =>
		albums ??= new EFCoreSet<Album>(this.InnerDomainContainer.Albums, this);
}
```

## Query Extensions

```csharp
using Grammophone.DataAccess.QueryExtensions;

var album = await musicDomainContainer.Albums
	.Include(a => a.Tracks)
	.ThenInclude(t => t.Genre)
	.AsNoTracking()
	.SingleAsync(a => a.Name == "Blue Integration");
```

The EF Core implementation receives native queryables in its terminal and shaping adapters. Implementers can delegate to EF Core APIs directly.

## Set-Based Mutations

EF Core supports portable set-based delete and update operations directly:

```csharp
var deleted = await musicDomainContainer.Tracks
	.Where(t => t.Album.Name == "Blue Integration")
	.ExecuteDeleteAsync();
```

```csharp
var updated = await musicDomainContainer.Tracks
	.Where(t => t.Album.Name == "Blue Integration")
	.ExecuteUpdateAsync(setters => setters
		.SetProperty(t => t.DurationSeconds, t => t.DurationSeconds + 5));
```

These operations execute immediately in the database. They do not materialize entities, do not use change tracking to update or delete individual entities and do not synchronize already-tracked instances.

## SQL Server Exception Translation

Configure SQL Server exception translation when creating the context:

```csharp
var innerContainer = new EFCoreMusicDomainContainer(options)
{
	ExceptionTransformer = new MicrosoftSqlServerExceptionTransformer()
};

var domainContainer = new EFCoreMusicDomainContainerAdapter(innerContainer);
```

Duplicate key errors become `UniqueConstraintViolationException`. Foreign key violations become `ReferentialConstraintViolationException`.

## Spatial Data

EF Core spatial support is not abstracted by this project. If both EF6 and EF Core must map the same SQL Server `geography` column during migration, use provider-specific properties mapped to the same column and ignore the inactive property in each provider model.
