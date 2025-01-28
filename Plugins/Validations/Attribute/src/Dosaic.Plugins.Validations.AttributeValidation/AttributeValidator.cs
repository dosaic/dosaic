using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Dosaic.Plugins.Validations.Abstractions;

namespace Dosaic.Plugins.Validations.AttributeValidation;

internal class AttributeValidator(IServiceProvider serviceProvider) : IValidator
{
    private static readonly IValueValidator _requiredValidator = new Validators.Validations.RequiredAttribute();

    private static readonly IDictionary<MemberInfo, IValueValidator[]>
        _validationCache = new ConcurrentDictionary<MemberInfo, IValueValidator[]>();

    public async Task<ValidationResult> ValidateAsync(object value, IList<IValueValidator> validators, CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext(value, "", serviceProvider);
        var errors = await RunValidationsAsync(context, validators, cancellationToken);
        errors.AddRange(await ValidateObjectAsync(context, cancellationToken));
        return new ValidationResult { Errors = errors.ToArray() };
    }

    public async Task<ValidationResult> ValidateAsync(object model, CancellationToken cancellationToken = default)
    {
        var context = new ValidationContext(model, "", serviceProvider);
        return new ValidationResult { Errors = await ValidateObjectAsync(context, cancellationToken) };
    }

    private static async Task<List<ValidationError>> RunValidationsAsync(ValidationContext context,
        IList<IValueValidator> validators, CancellationToken cancellationToken)
    {
        if (ContainsValidator<Validators.Validations.RequiredAttribute>(validators))
        {
            if (!await _requiredValidator.IsValidAsync(context, cancellationToken))
            {
                return [CreateRequiredError(context.Path)];
            }
        }
        var errors = new List<ValidationError>();
        foreach (var validator in validators)
        {
            try
            {
                var result = await validator.IsValidAsync(context, cancellationToken);
                if (result) continue;
                var error = new ValidationError
                {
                    Path = context.Path,
                    Arguments = validator.GetArguments(),
                    Code = validator.Code,
                    Validator = validator.GetName(),
                    Message = validator.ErrorMessage
                };
                errors.Add(error);
            }
            catch (Exception)
            {
                var error = new ValidationError
                {
                    Path = context.Path,
                    Arguments = validator.GetArguments(),
                    Code = ValidationCodes.GenericError,
                    Validator = validator.GetName(),
                    Message = "Could not validate"
                };
                errors.Add(error);
            }
        }

        return errors;
    }

    private async Task<ValidationError[]> ValidateObjectAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        var validators = GetValidators(context.ValueType);
        var errors = await RunValidationsAsync(context, validators, cancellationToken);
        if (context.IsNullValue || !context.IsObjectType) return errors.ToArray();
        foreach (var property in context.ValueType.GetProperties())
        {
            var value = property.GetValue(context.Value);
            var propContext = context with { Value = value, Path = NextPath(context.Path, property.Name) };
            var propValidators = GetValidators(property);
            errors.AddRange(await RunValidationsAsync(propContext, propValidators, cancellationToken));
            if (propContext.IsNullValue)
                continue;
            if (propContext.IsObjectType)
                errors.AddRange(await ValidateObjectAsync(propContext, cancellationToken));
            else if (propContext.IsListType)
                errors.AddRange(await ValidateListAsync(propContext, cancellationToken));
        }
        return errors.ToArray();
    }

    private async Task<ValidationError[]> ValidateListAsync(ValidationContext context, CancellationToken cancellationToken)
    {
        if (context.IsNullValue) return [];
        var enumerable = (IEnumerable)context.Value!;
        var errors = new List<ValidationError>();
        var index = 0;
        foreach (var item in enumerable)
        {
            var itemContext = context with { Value = item, Path = NextPath(context.Path, index.ToString(CultureInfo.InvariantCulture)) };
            if (itemContext.IsNullValue) continue;
            if (itemContext.IsObjectType)
                errors.AddRange(await ValidateObjectAsync(itemContext, cancellationToken));
            else if (itemContext.IsListType)
                errors.AddRange(await ValidateListAsync(itemContext, cancellationToken));
            index++;
        }
        return errors.ToArray();
    }

    private static IValueValidator[] GetValidators(Type type)
    {
        if (type is null) return [];
        if (_validationCache.TryGetValue(type, out var validators))
            return validators;
        return _validationCache[type] = type.GetCustomAttributes(typeof(IValueValidator), true).OfType<IValueValidator>().ToArray();
    }

    private static IValueValidator[] GetValidators(PropertyInfo property)
    {
        if (_validationCache.TryGetValue(property, out var validators))
            return validators;
        return _validationCache[property] = property.GetCustomAttributes(typeof(IValueValidator), true).OfType<IValueValidator>().ToArray();
    }
    private static bool ContainsValidator<T>(IEnumerable<IValueValidator> validators) where T : IValueValidator =>
        validators.Any(i => i.GetType() == typeof(T));

    private static ValidationError CreateRequiredError(string path) => new()
    {
        Path = path,
        Arguments = new Dictionary<string, object>(),
        Code = _requiredValidator.Code,
        Validator = _requiredValidator.GetName(),
        Message = _requiredValidator.ErrorMessage
    };

    private static string NextPath(string basePath, string name) => string.IsNullOrEmpty(basePath) ? name : $"{basePath}/{name}";
}
