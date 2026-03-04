# Dosaic.Plugins.Handlers.Abstractions.Cqrs

Defines the CQRS handler and validator contracts for the Dosaic plugin framework.
This package contains the core interfaces and models that resource handlers must
implement — it has no runtime dependencies beyond FluentValidation and the Dosaic
persistence/extensions abstractions, making it the right lightweight reference for
libraries that need to declare handlers without pulling in full implementations.

## Installation

```bash
dotnet add package Dosaic.Plugins.Handlers.Abstractions.Cqrs
```

## Features

### Handler interfaces (`Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers`)

| Interface | Method | Description |
|---|---|---|
| `IHandler` | — | Marker interface; all handlers must implement this |
| `ICreateHandler<TResource>` | `CreateAsync(TResource, CancellationToken)` | Creates a new resource and returns it |
| `IUpdateHandler<TResource>` | `UpdateAsync(TResource, CancellationToken)` | Updates an existing resource and returns it |
| `IDeleteHandler<TResource>` | `DeleteAsync(IGuidIdentifier, CancellationToken)` | Deletes a resource by its identifier |
| `IGetHandler<TResource>` | `GetAsync(IGuidIdentifier, CancellationToken)` | Retrieves a single resource by identifier |
| `IGetListHandler<TResource>` | `GetListAsync(PagingRequest, CancellationToken)` | Returns a paged list of resources |

### Validator interfaces (`Dosaic.Plugins.Handlers.Abstractions.Cqrs.Validators`)

| Interface | Method | Description |
|---|---|---|
| `IBaseValidator` | — | Marker interface for all validators |
| `ICreateValidator<TResource>` | `ValidateOnCreate(AbstractValidator<TResource>)` | Validation rules applied before create |
| `IUpdateValidator<TResource>` | `ValidateOnUpdate(AbstractValidator<TResource>)` | Validation rules applied before update |
| `IDeleteValidator<TResource>` | `ValidateOnDelete(AbstractValidator<TResource>)` | Validation rules applied before delete |
| `IGetValidator<TResource>` | `ValidateOnGet(AbstractValidator<TResource>)` | Validation rules applied before get |
| `IGetListValidator<TResource>` | `ValidateOnGetList(AbstractValidator<TResource>)` | Validation rules applied before list |
| `GenericValidator<T>` | — | Concrete `AbstractValidator<T>` that delegates rule setup to an `Action<AbstractValidator<T>>` |

### Models (`Dosaic.Plugins.Handlers.Abstractions.Cqrs.Models`)

| Type | Description |
|---|---|
| `GuidIdentifier` | Record implementing `IGuidIdentifier`. Has static `Empty`, `New`, and `Parse(string)` factory members |

### Enum

| Type | Values |
|---|---|
| `HandlerAction` | `Get`, `GetList`, `Create`, `Update`, `Delete` |

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

### 2. Implement a validator

A single class can implement multiple validator interfaces, grouping all validation
logic for a resource in one place:

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

### 3. Implement custom handlers

Use the handler interfaces when the built-in `SimpleResource` implementations
(from `Dosaic.Plugins.Handlers.Cqrs`) do not fit your needs:

```csharp
using Dosaic.Plugins.Handlers.Abstractions.Cqrs.Handlers;
using Dosaic.Plugins.Persistence.Abstractions;

public class ArticleCreateHandler : ICreateHandler<Article>, IHandler
{
    private readonly IRepository<Article> _repository;
    private readonly ICreateValidator<Article> _validator;

    public ArticleCreateHandler(
        IRepository<Article> repository,
        ICreateValidator<Article> validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public async Task<Article> CreateAsync(Article request, CancellationToken cancellationToken)
    {
        // validate using a GenericValidator that delegates to the injected validator
        var fluentValidator = new GenericValidator<Article>(_validator.ValidateOnCreate);
        await fluentValidator.ValidateOrThrowAsync(request, cancellationToken);

        request.Id = Guid.NewGuid();
        return await _repository.AddAsync(request, cancellationToken);
    }
}
```

### 4. Use `GuidIdentifier` for request objects

`GuidIdentifier` is a ready-made implementation of `IGuidIdentifier` that can be
used wherever a handler expects just an identifier:

```csharp
// Create a new random identifier
var id = GuidIdentifier.New;

// Parse from a string (e.g. from a route parameter)
var id = GuidIdentifier.Parse("05a82a17-5a7f-4f28-8ff9-37f35c2cfb5f");

// Represent an empty/null identifier
var empty = GuidIdentifier.Empty;
```

### 5. Use the `Dosaic.Plugins.Handlers.Cqrs` implementation plugin (recommended)

If your resource has a repository registered in the DI container, you can skip
writing handlers altogether by adding the `CqrsSimpleResourcePlugin` from
`Dosaic.Plugins.Handlers.Cqrs`. It automatically registers generic
`SimpleResource*Handler<T>` implementations for all five CQRS operations:

```csharp
// The plugin detects all IHandler and IBaseValidator implementations in your
// assemblies and registers them, including the generic CRUD handlers.
// No manual registration is required — just run the Dosaic host:
PluginWebHostBuilder.RunDefault(DosaicPluginTypes.All);
```

Resolved handler types:

| Handler interface | Default implementation |
|---|---|
| `ICreateHandler<TResource>` | `SimpleResourceCreateHandler<TResource>` |
| `IUpdateHandler<TResource>` | `SimpleResourceUpdateHandler<TResource>` |
| `IDeleteHandler<TResource>` | `SimpleResourceDeleteHandler<TResource>` |
| `IGetHandler<TResource>` | `SimpleResourceGetHandler<TResource>` |
| `IGetListHandler<TResource>` | `SimpleResourceGetListHandler<TResource>` |

Custom implementations registered in your own assemblies take precedence because
the plugin scans for types that implement `IHandler` across all non-framework
assemblies and registers them on top of the defaults.

## Dependencies

| Package | Purpose |
|---|---|
| `Dosaic.Extensions.Abstractions` | `PagedList<T>` used in `IGetListHandler` |
| `Dosaic.Plugins.Persistence.Abstractions` | `IGuidIdentifier`, `PagingRequest` |
| `FluentValidation` | `AbstractValidator<T>` used in validator interfaces |
