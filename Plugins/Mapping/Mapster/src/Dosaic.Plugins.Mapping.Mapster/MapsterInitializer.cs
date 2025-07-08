using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Mapster;

namespace Dosaic.Plugins.Mapping.Mapster
{
    internal static class MapsterInitializer
    {
        private static bool HasMapFrom(PropertyInfo propertyInfo) =>
        propertyInfo.GetCustomAttributes().Any(ca => ca.GetType().Name.StartsWith("MapFrom"));

        private static (string[] Paths, Type Source) GetNavigation(PropertyInfo propertyInfo)
        {
            var mapFromAttribute = propertyInfo.GetCustomAttributes().FirstOrDefault(ca => ca.GetType().Name.StartsWith("MapFrom"));
            if (mapFromAttribute is null) return ([propertyInfo.Name], propertyInfo.DeclaringType!);
            var navProps = mapFromAttribute.GetType().GetProperty(nameof(MapFromAttribute<MapsterPlugin>.NavigationProperties))!;
            var sourceType = mapFromAttribute.GetType().GetProperty(nameof(MapFromAttribute<MapsterPlugin>.Source))!;
            var navigation = navProps.GetValue(mapFromAttribute) as string[] ?? [propertyInfo.Name];
            var source = sourceType.GetValue(mapFromAttribute) as Type ?? propertyInfo.DeclaringType!;
            return (navigation, source);
        }

        public static void InitMapster(Assembly[] assemblies)
        {
            var typesWithMappingAttribute = assemblies.SelectMany(a => a.GetTypes())
                .Where(x => x.GetProperties().Any(HasMapFrom))
                .ToList();
            foreach (var type in typesWithMappingAttribute)
            {
                var props = type.GetProperties().Where(HasMapFrom).ToList();
                foreach (var prop in props)
                {
                    var (paths, sourceType) = GetNavigation(prop);
                    var parameter = Expression.Parameter(sourceType, "src");
                    var body = BuildExpressionTree(parameter, paths, prop.PropertyType);
                    var lambda = Expression.Lambda(body, parameter);
                    var propParam = Expression.Parameter(type, "src");
                    var propAccess = Expression.PropertyOrField(propParam, prop.Name);
                    var propLambda = Expression.Lambda(propAccess, propParam);
                    var registerMappingMethod = typeof(MapsterInitializer).GetMethod(nameof(RegisterMapping), BindingFlags.Static | BindingFlags.NonPublic);
                    registerMappingMethod!.MakeGenericMethod(sourceType, type, prop.PropertyType).Invoke(null, [propLambda, lambda]);
                }
            }
        }

        private static void RegisterMapping<TSource, TTarget, TProp>(Expression<Func<TTarget, TProp>> targetProperty, Expression<Func<TSource, TProp>> mapping)
        {
            var config = TypeAdapterConfig.GlobalSettings.ForType<TSource, TTarget>();
            config.Map(targetProperty, mapping);
        }

        private static Expression BuildExpressionTree(Expression parameter, string[] paths, Type targetType)
        {
            var body = parameter;

            for (var i = 0; i < paths.Length; i++)
            {
                var currentProperty = body.Type.GetProperty(paths[i]) ?? throw new InvalidOperationException($"Property '{paths[i]}' not found on type '{body.Type}'");

                if (typeof(IEnumerable).IsAssignableFrom(currentProperty.PropertyType) && currentProperty.PropertyType != typeof(string))
                {
                    var elementType = currentProperty.PropertyType.GetGenericArguments().FirstOrDefault() ?? throw new InvalidOperationException($"Cannot determine the element type of the collection '{currentProperty.Name}'");
                    var collectionParameter = Expression.Parameter(elementType, "x");
                    var targetElementType = targetType.GetGenericArguments().FirstOrDefault();

                    if (targetElementType == null)
                        throw new InvalidOperationException($"Target element type '{targetType.Name}' cannot be determined for the collection of the source property '{currentProperty.Name}'");

                    var collectionBody = BuildExpressionTree(collectionParameter, paths.Skip(i + 1).ToArray(), targetElementType);

                    var collectionAdaptMethod = typeof(TypeAdapter).GetMethod(nameof(TypeAdapter.Adapt), new[] { typeof(object) })!
                                                          .MakeGenericMethod(targetElementType);
                    var adaptCall = Expression.Call(collectionAdaptMethod, collectionBody);

                    var collectionLambda = Expression.Lambda(adaptCall, collectionParameter);
                    var selectMethod = typeof(Enumerable).GetMethods()
                        .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                        .MakeGenericMethod(elementType, targetElementType);

                    var propAccess = Expression.PropertyOrField(body, paths[i]);
                    var selectCall = Expression.Call(selectMethod, propAccess, collectionLambda);
                    var nullCheck = Expression.Equal(propAccess, Expression.Constant(null, propAccess.Type));
                    var conditional = Expression.Condition(nullCheck,
                        Expression.Constant(null, typeof(IEnumerable<>).MakeGenericType(targetElementType)),
                        selectCall);

                    body = conditional;
                    break;
                }
                body = Expression.PropertyOrField(body, paths[i]);
                if (i != paths.Length - 1 || body.Type == targetType)
                    continue;
                var adaptMethod = typeof(TypeAdapter).GetMethod(nameof(TypeAdapter.Adapt), new[] { typeof(object) })!
                    .MakeGenericMethod(targetType);
                body = Expression.Call(adaptMethod, body);
            }

            return body;
        }
    }
}
