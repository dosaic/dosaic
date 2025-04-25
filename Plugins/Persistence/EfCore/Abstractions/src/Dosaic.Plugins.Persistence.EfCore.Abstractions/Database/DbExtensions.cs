using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Chronos.Abstractions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Microsoft.EntityFrameworkCore;

namespace Dosaic.Plugins.Persistence.EfCore.Abstractions.Database
{
    public static class DbExtensions
    {
        public static async Task<ImmutableArray<TAggregate>> GetEvents<TAggregate>(this IDb db, TAggregate aggregate,
            IDateTimeProvider dateTimeProvider)
            where TAggregate : AggregateEvent
        {
            var result = await db.GetQuery<TAggregate>()
                .Where(x => !x.IsDeleted && x.ValidFrom <= dateTimeProvider.UtcNow)
                .Where(BuildLambdaExpression(aggregate))
                .ToArrayAsync();
            return [.. result];
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
                    var propertyAccess = Expression.Property(parameter, p.Name);
                    Expression<Func<object>> valueExpr = () => propertyValue;
                    var valueExpression = Expression.Convert(valueExpr.Body, p.PropertyType);
                    return Expression.Equal(propertyAccess, valueExpression);
                }).ToArray();

            if (!expressions.Any())
                return x => true;

            var body = expressions.Aggregate(Expression.AndAlso);
            return Expression.Lambda<Func<TAggregate, bool>>(body, parameter);
        }
    }
}
