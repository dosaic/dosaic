# Dosaic.Plugins.Handlers.Cqrs

Dosaic.Plugins.Handlers.Cqrs is a `plugin` that provides CQRS implementation for basic resource operations, automatically registering handlers and validators for CRUD operations.

## Installation

To install the nuget package follow these steps:

```shell
dotnet add package Dosaic.Plugins.Handlers.Cqrs
```

## Appsettings.yml

No specific configuration in appsettings.yml is required for this plugin.

## Configuration in your plugin host

No specific configuration available.


## Features

- Automatic registration of default CRUD handlers:
    - Create (`ICreateHandler<>`)
    - Update (`IUpdateHandler<>`)
    - Delete (`IDeleteHandler<>`)
    - Get (`IGetHandler<>`)
    - GetList (`IGetListHandler<>`)

- Auto-discovery and registration of custom handlers implementing `IHandler`
- Auto-discovery and registration of custom validators implementing `IBaseValidator`

___
## Example Usage
Example service using the create handler, all handlers are to be used in the same way

```c#
public class UserService
{
    private readonly ICreateHandler<UserResource> _createHandler;

    public UserService(ICreateHandler<UserResource> createHandler)
    {
        _createHandler = createHandler;
    }

    public async Task<UserResource> CreateUserAsync(string name, CancellationToken cancellationToken)
    {
        var newUser = new UserResource { Name = name };
        return await _createHandler.CreateAsync(newUser, cancellationToken);
    }
}

// Example validator
public class UserResourceValidator : ICreateValidator<UserResource>
{
    public void ValidateOnCreate(UserResource resource)
    {
        if (string.IsNullOrEmpty(resource.Name))
            throw new ValidationException("Name is required");
    }
}

// Example repository implementation or use a Dosaic.Plugins.Persistence plugin for a database of your choice
public class UserResourceRepository : IRepository<UserResource>
{
    public async Task<UserResource> AddAsync(UserResource entity, CancellationToken cancellationToken)
    {
        // Implementation details...
        return entity;
    }

    // Other repository methods...
}
```


