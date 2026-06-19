# Entity Framework Core Setup

This project adapts EF Core 8 to the `Grammophone.DataAccess` contracts.

## Domain Container

Define a domain container by deriving from `EFCoreDomainContainer`:

```csharp
public class EFCoreMusicDomainContainer : EFCoreDomainContainer
{
	public EFCoreMusicDomainContainer(DbContextOptions options)
		: base(options)
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

`EFCoreDomainContainer.OnConfiguring` enables lazy-loading and change-tracking proxies. Derived contexts overriding `OnConfiguring` must call `base.OnConfiguring(optionsBuilder)` if `IDomainContainer.Create<T>()` is expected to create proxy instances.

Application code should use `IDomainContainer.Create<T>()` or `IEntitySet<T>.Create()` for new entities, not provider-specific factory APIs.

## Options

Configure EF Core normally:

```csharp
var options = new DbContextOptionsBuilder<EFCoreMusicDomainContainer>()
	.UseSqlServer(connectionString)
	.Options;
```

The base container configures proxy support. Entity classes must satisfy EF Core proxy requirements. With change-tracking proxies enabled, every mapped property must be `virtual` without exception, including scalar properties, key properties, reference navigations and collection navigations. Collection navigation implementations must support change notifications, for example by using `ObservableCollection<T>`.

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
