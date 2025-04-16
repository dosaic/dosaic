using System.Linq.Expressions;
using System.Reflection;
using Chronos.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public static class DbExtensions
    {
        public static IQueryable<TAggregate> GetEvents<TAggregate>(this IDb db, TAggregate aggregate,
            IDateTimeProvider dateTimeProvider)
            where TAggregate : AggregateEvent
        {
            return db.GetQuery<TAggregate>().Where(BuildLambdaExpression(aggregate))
                .Where(x => !x.IsDeleted && x.ValidFrom <= dateTimeProvider.UtcNow);
        }

        private static Expression<Func<TAggregate, bool>> BuildLambdaExpression<TAggregate>(TAggregate entity)
        {
            var parameter = Expression.Parameter(typeof(TAggregate), "model");
            var properties = typeof(TAggregate).GetProperties()
                .Where(x => x.GetCustomAttribute<EventMatcherAttribute>() != null)
                .ToArray();

            var expressions = properties
                .Select(p =>
                {
                    var propertyValue = p.GetValue(entity);

                    // Create a member access expression for the property
                    var propertyAccess = Expression.Property(parameter, p.Name);

                    // Create a parameterized expression for the value instead of a constant
                    Expression<Func<object>> valueExpr = () => propertyValue;
                    var valueExpression = Expression.Convert(valueExpr.Body, p.PropertyType);

                    // Create an equality comparison
                    return Expression.Equal(propertyAccess, valueExpression);
                }).ToArray();

            if (!expressions.Any())
                return x => true;

            var body = expressions.Aggregate(Expression.AndAlso);
            return Expression.Lambda<Func<TAggregate, bool>>(body, parameter);
        }

        // Helper class to replace parameters in expressions
        private class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                return ReferenceEquals(node, _oldParameter) ? _newParameter : base.VisitParameter(node);
            }
        }
    }
}
