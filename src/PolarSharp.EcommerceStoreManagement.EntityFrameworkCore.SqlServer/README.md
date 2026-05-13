# PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.SqlServer

SQL Server provider for catalog persistence. Adds `.UseSqlServer(connStr)` extension; ships RLS migrations and EF Core health checks.

```csharp
builder.Services
    .AddPolarEcommerce()
    .UseSqlServer(connStr);
```

## License

MIT.
