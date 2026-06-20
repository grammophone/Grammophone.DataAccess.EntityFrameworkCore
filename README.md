# Grammophone.DataAccess.EntityFrameworkCore

`Grammophone.DataAccess.EntityFrameworkCore` is the Entity Framework Core 8 implementation of the `Grammophone.DataAccess` abstractions.

It targets .NET 8 and adapts EF Core `DbContext`, `DbSet<T>`, query execution, query shaping, exception translation and proxy-based entity creation to the provider-neutral contracts.

## Main Features

- `EFCoreDomainContainer` derives from EF Core `DbContext` and implements `IDomainContainer`.
- `EFCoreDomainContainerAdapter<T>` adapts EF Core contexts to provider-neutral domain interfaces.
- `EFCoreSet<T>` and `EFCoreQuery<T, Q>` adapt `DbSet<T>` and queryables to `IEntitySet<T>` and `IEntityQuery<T>`.
- `EFCoreTranslatingQueryProvider` preserves the query abstraction through standard LINQ composition.
- `EFCoreQueryTranslatorFactory` maps portable functions to EF Core and SQL Server functions.
- `EFCoreTerminalMethodsAdapter` delegates async terminal methods to EF Core async query APIs.
- `EFCoreShapingMethodsAdapter` delegates executable shaping operations such as `Include` and `AsNoTracking` to EF Core.
- `EFCoreSetOperationMethodsAdapter` delegates set-based mutations such as `ExecuteDelete` and `ExecuteUpdate` to EF Core.
- `MicrosoftSqlServerExceptionTransformer` normalizes SQL Server provider errors into portable exceptions.
- Proxy creation is enabled through EF Core lazy-loading and change-tracking proxies.

## Usage Shape

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
}
```

```csharp
public class EFCoreMusicDomainContainerAdapter :
	EFCoreDomainContainerAdapter<EFCoreMusicDomainContainer>,
	IMusicDomainContainer
{
	private IEntitySet<Album> albums;

	public IEntitySet<Album> Albums =>
		albums ??= new EFCoreSet<Album>(this.InnerDomainContainer.Albums, this);
}
```

Application code can then use the same `IMusicDomainContainer` contract as the EF6 implementation.

## Documentation

- [Entity Framework Core setup](documentation/setup.md)

## Related Projects

- `Grammophone.DataAccess` defines the provider-neutral contracts.
- `Grammophone.DataAccess.EntityFramework` provides the EF6 implementation.
