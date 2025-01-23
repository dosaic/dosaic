# Dosaic.Plugins.Handlers.Abstractions.Cqrs


## ICreateHandler
An interface definition for a generic Create handler in a CQRS pattern. It defines a contract for creating resources of type `TResource` asynchronously.

___
### Example Usage
```c#
public class UserCreateHandler : ICreateHandler<User>
{
    private readonly IUserRepository _repository;
    public async Task<User> CreateAsync(User request, CancellationToken cancellationToken)
    {
        // Implementation for creating a new user
        return await _repository.CreateUserAsync(request, cancellationToken);
    }
}
```

## IDeleteHandler
An interface defining a generic delete handler for resources with GUID identifiers. The interface includes a method for asynchronous deletion operations.

___
### Example Usage
```c#
public class UserDeleteHandler : IDeleteHandler<User>
{
    private readonly IUserRepository _repository;
    public async Task DeleteAsync(IGuidIdentifier request, CancellationToken cancellationToken)
    {
        // Delete user logic here
        await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
```

## IGetHandler
Generic interface defining a handler for retrieving a single resource by its GUID identifier asynchronously.

___
### Example Usage
```c#
public class UserGetHandler : IGetHandler<User>
{
    private readonly IUserRepository _repository;
    public async Task<User> GetAsync(IGuidIdentifier request, CancellationToken cancellationToken)
    {
        // Implementation to fetch user by ID
        return await _repository.GetUserByIdAsync(request.Id, cancellationToken);
    }
}
```

## IGetListHandler
Interface defining a handler for retrieving paginated lists of resources, implementing the CQRS pattern.

___
### Example Usage
```c#
public class UserListHandler : IGetListHandler<UserResource>
{
    private readonly IUserRepository _repository;
    public async Task<PagedList<UserResource>> GetListAsync(PagingRequest request, CancellationToken cancellationToken)
    {
        // Implementation to fetch paginated user resources
        var users = await _repository.GetUsersAsync(request.Page, request.PageSize, cancellationToken);
        return new PagedList<UserResource>(users, request.Page, request.PageSize, totalCount);
    }
}
```

## IUpdateHandler
An interface definition for handling update operations in a CQRS pattern. It extends `IHandler` and defines a generic update method for a specific resource type.
___
### Example Usage
```c#
public class UserUpdateHandler : IUpdateHandler<User>
{
    private readonly IUserRepository _repository;
    public async Task<User> UpdateAsync(User request, CancellationToken cancellationToken)
    {
        // Implementation for updating user
        return await UpdateUserInDatabase(request, cancellationToken);
    }
}
```

## ICreateValidator
Interface defining a validator for create operations, extending `IBaseValidator`. It provides a method to validate resources during creation using FluentValidation.

___
### Example Usage
```c#
public class UserCreateValidator : ICreateValidator<UserResource>
{
    public void ValidateOnCreate(AbstractValidator<UserResource> validator)
    {
        validator.RuleFor(x => x.Name).NotEmpty();
        validator.RuleFor(x => x.Email).EmailAddress();
    }
}
```

## IGetValidator
Interface defining a validator for get operations, extending `IBaseValidator`. It provides a method to validate resources during retrieval using FluentValidation.

___
### Example Usage
```c#
public class UserGetValidator : IGetValidator<UserResource>
{
    public void ValidateOnGet(AbstractValidator<UserResource> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.IsActive).Equal(true);
    }
}
```

## IGetListValidator
Interface defining a validator for list operations, extending `IBaseValidator`. It provides a method to validate resources during list retrieval using FluentValidation.

___
### Example Usage
```c#
public class UserListValidator : IGetListValidator<UserResource>
{
    public void ValidateOnGetList(AbstractValidator<UserResource> validator)
    {
        validator.RuleFor(x => x.PageSize).GreaterThan(0).LessThanOrEqualTo(100);
        validator.RuleFor(x => x.Page).GreaterThan(0);
    }
}
```

## IUpdateValidator
Interface defining a validator for update operations, extending `IBaseValidator`. It provides a method to validate resources during updates using FluentValidation.

___
### Example Usage
```c#
public class UserUpdateValidator : IUpdateValidator<UserResource>
{
    public void ValidateOnUpdate(AbstractValidator<UserResource> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.LastModified).NotEmpty();
        validator.RuleFor(x => x.Version).NotEmpty();
    }
}
```

## IDeleteValidator
Interface defining a validator for delete operations, extending `IBaseValidator`. It provides a method to validate resources during deletion using FluentValidation.

___
### Example Usage
```c#
public class UserDeleteValidator : IDeleteValidator<UserResource>
{
    public void ValidateOnDelete(AbstractValidator<UserResource> validator)
    {
        validator.RuleFor(x => x.Id).NotEmpty();
        validator.RuleFor(x => x.CanBeDeleted).Equal(true);
    }
}
```











