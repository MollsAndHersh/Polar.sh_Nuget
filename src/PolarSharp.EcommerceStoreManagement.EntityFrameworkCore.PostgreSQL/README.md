# PolarSharp.EcommerceStoreManagement.EntityFrameworkCore.PostgreSQL

PostgreSQL provider for catalog persistence. Adds `.UsePostgreSql(connStr)` extension; ships RLS migrations and EF Core health checks.

```csharp
builder.Services
    .AddPolarEcommerce()
    .UsePostgreSql(connStr);
```

## License

MIT.
