# Dosaic.Plugins.Handlers.Cqrs

Provides concrete CQRS handler implementations for the Dosaic plugin framework.
This package ships five ready-to-use generic `SimpleResource` handlers that cover
the full CRUD lifecycle for any resource type that implements `IGuidIdentifier`.
It also auto-discovers and registers your own custom handlers and validators
through the Dosaic `IImplementationResolver`, and integrates built-in
OpenTelemetry tracing and FluentValidation support.

> Use [`Dosaic.Plugins.Handlers.Abstractions.Cqrs`](../Abstractions/Cqrs/README.md)
> if you only need the handler and validator contracts without the concrete implementations.

## Installation

```bash
dotnet add package Dosaic.Plugins.Handlers.Cqrs
```

## Features

- **Five generic CRUD handlers** — `Create`, `Update`, `Delete`, `Get`, and `GetList`
  covering the complete resource lifecycle out of the box
- **Automatic GUID assignment** on create — the handler assigns a fresh `Guid.NewGuid()` so callers never have to supply an ID
- **Existence check on update** — throws a `DosaicException` if the resource does not exist before attempting the update
- **Paged list support** — `GetListAsync` parses `PagingRequest` into `QueryOptions`, runs count and data queries in parallel, and returns a `PagedList<TResource>`
- **Integrated validation** — every handler validates its input with FluentValidation and throws `ValidationDosaicException` on failure, giving structured `FieldValidationError` details to callers
- **Auto-discovery of custom handlers and validators** — at plugin startup, `CqrsSimpleResourcePlugin` scans for all types implementing `IHandler` or `IBaseValidator` and registers them in DI
- **Handler override** — register your own `ICreateHandler<T>` (or any other handler interface) implementation and that registration takes precedence over the built-in `SimpleResource*` handler
- **OpenTelemetry tracing** — every handler operation starts a named `Activity` with resource-relevant tags (e.g. `resource-id`) for distributed trace correlation
- **`ValidateOrThrowAsync` extension** — reusable `IValidator<T>` extension that throws `ValidationDosaicException` instead of returning a result, with null-request guard included

## Usage

### 1. Define your resource model

Your resource must implement `IGuidIdentifier` from `Dosaic.Plugins.Persistence.Abstractions`:

```csharp
using Dosaic.Plugins.Persistence.Abstractions;

public class Article : IGuidIdentifier
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
}
```

### 2. Register the plugin

Add `CqrsSimpleResourcePlugin` to your Dosaic host. Because Dosaic uses source
generation, simply referencing this package in your host project is enough —
`DosaicPluginTypes.All` emitted by the generator will include it automatically.

```csharp
using Dosaic.Hosting.WebHost;

PluginWebHostBuilder.RunDefault(Dosaic.Generated.DosaicPluginTypes.All);
```

When `CqrsSimpleResourcePlugin.ConfigureServices` runs it registers:

| Service | Implementation |
|---|---|
| `ICreateHandler<TResource>` | `SimpleResourceCreateHandler<TResource>` |
| `IUpdateHandler<TResource>` | `SimpleResourceUpdateHandler<TResource>` |
| `IDeleteHandler<TResource>` | `SimpleResourceDeleteHandler<TResource>` |
| `IGetHandler<TResource>` | `SimpleResourceGetHandler<TResource>` |
| `IGetListHandler<TResource>` | `SimpleResourceGetListHandler<TResource>` |
| Any `IHandler` implementation found by the resolver | the discovered type |
| Any `IBaseValidator` implementation found by the resolver | the discovered type |

All registrations use `AddTransient` lifetime.

### 3. Provide a repository

The built-in handlers depend on `IRepository<TResource>` and
`IReadRepository<TResource>` from `Dosaic.Plugins.Persistence.Abstractions`.
Register a concrete persistence plugin (e.g. `Dosaic.Plugins.Persistence.EfCore`)
alongside this plugin, or provide your own implementation:

```csharp
services.AddSingleton<IRepository<Article>, ArticleRepository>();
services.AddSingleton<IReadRepository<Article>>(sp =>
    sp.GetRequiredService<IRepository<Article>>() as IReadRepository<Article>);
```

### 4. Resolve and use the handlers

Inject the handler interfaces where you need them — for example inside a Dosaic
endpoint, a controller, or any other DI-resolved service:

```csharp
public class ArticleEndpoint(
    ICreateHandler<Article> createHandler,
    IUpdateHandler<Article> updateHandler,
    IDeleteHandler<Article> deleteHandler,
    IGetHandler<Article> getHandler,
    IGetListHandler<Article> getListHandler)
{
    // Create — ID is auto-assigned by the handler
    public Task<Article> CreateAsync(Article article, CancellationToken ct)
        => createHandler.CreateAsync(article, ct);

    // Update — throws DosaicException when the article does not exist
    public Task<Article> UpdateAsync(Article article, CancellationToken ct)
        => updateHandler.UpdateAsync(article, ct);

    // Delete — requires a non-empty Guid
    public Task DeleteAsync(Guid id, CancellationToken ct)
        => deleteHandler.DeleteAsync(new GuidIdentifier(id), ct);

    // Get by ID — requires a non-empty Guid
    public Task<Article> GetAsync(Guid id, CancellationToken ct)
        => getHandler.GetAsync(new GuidIdentifier(id), ct);

    // Paged list — page/size are optional; size is capped at 100
    public Task<PagedList<Article>> ListAsync(PagingRequest paging, CancellationToken ct)
        => getListHandler.GetListAsync(paging, ct);
}
```

### 5. Add custom validation

Implement one or more validator interfaces from
`Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators` and make the class public.
The plugin discovers it automatically at startup and registers it against each
interface it implements:

```csharp
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators;
using FluentValidation;

public class ArticleValidator
    : ICreateValidator<Article>,
      IUpdateValidator<Article>,
      IBaseValidator
{
    public void ValidateOnCreate(AbstractValidator<Article> validator)
    {
        validator.RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        validator.RuleFor(x => x.Body).NotEmpty();
    }

    public void ValidateOnUpdate(AbstractValidator<Article> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}
```

`SimpleResourceCreateHandler<Article>` automatically receives the
`ICreateValidator<Article>` from DI and wraps it into a `GenericValidator<Article>`
via `ValidateOnCreate`. The same pattern applies for update.

### 6. Override a built-in handler

Create a public class that implements the relevant handler interface and register
it anywhere in your assembly. The plugin scans for `IHandler` implementations and
registers them after the built-in ones, so the last registration wins for
`AddTransient`:

```csharp
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Persistence.Abstractions;

public class CustomArticleDeleteHandler : IDeleteHandler<Article>, IHandler
{
    private readonly IRepository<Article> _repository;

    public CustomArticleDeleteHandler(IRepository<Article> repository)
        => _repository = repository;

    public async Task DeleteAsync(IGuidIdentifier request, CancellationToken ct)
    {
        // custom pre-delete logic, e.g. soft-delete
        var article = await _repository.FindByIdAsync(request.Id, ct);
        article.DeletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(article, ct);
    }
}
```

### 7. Use `ValidateOrThrowAsync` in your own handlers

The `ValidationExtensions.ValidateOrThrowAsync<T>` extension method is available
for any `IValidator<T>`. It throws `ValidationDosaicException` with structured
`FieldValidationError` details when validation fails, and guards against `null`
requests:

```csharp
using Dosaic.Plugins.Handlers.Cqrs.SimpleResource.Validators;

public class MyService(IValidator<Article> validator)
{
    public async Task ProcessAsync(Article article, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(article, ct); // throws on failure
        // proceed with business logic
    }
}
```

## Built-in Validation Rules

### `GuidIdentifierValidator`

Applied automatically by `SimpleResourceDeleteHandler` and `SimpleResourceGetHandler`:

| Field | Rule |
|---|---|
| `Id` | Must not be empty (`Guid.Empty`) |

### `PagingRequestValidator`

Applied automatically by `SimpleResourceGetListHandler`:

| Field | Rule |
|---|---|
| `Page` | When provided: must be ≥ 0 |
| `Size` | When provided: must be > 0 and ≤ 100 |
| `Sort` | When provided: must not be an empty string |
| `Filter` | When provided: must not be an empty string |
