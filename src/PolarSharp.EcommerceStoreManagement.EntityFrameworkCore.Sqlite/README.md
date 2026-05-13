# PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.Sqlite

SQLite provider for catalog persistence. Each tenant's catalog lives in its own `{tenantId}.db` file. Adds `.UseSqlite(directory)` extension.

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlite("/var/lib/polarsharp/catalog/");
```

## License

MIT.
