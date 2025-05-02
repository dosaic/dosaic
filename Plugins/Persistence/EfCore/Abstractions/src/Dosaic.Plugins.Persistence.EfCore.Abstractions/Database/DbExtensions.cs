using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Reflection;
using Chronos.Abstractions;
using Dosaic.Hosting.Abstractions.Extensions;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Audit;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Eventsourcing;
using Dosaic.Plugins.Persistence.EfCore.Abstractions.Models;
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

        private static IQueryable<T> IncludeProperties<T>(this DbSet<T> dbSet, string[] properties)
            where T : class, IModel
        {
            if (properties.Length == 0) return dbSet;
            var qry = dbSet.Include(properties[0]);
            for (var i = 1; i < properties.Length; i++)
            {
                qry = qry.Include(properties[i]);
            }

            return qry;
        }

        public static async Task UpdateGraphAsync<T>(this IDb db, T model, Expression<Func<T, bool>> findExisting,
            CancellationToken cancellationToken = default) where T : class, IModel
        {
            var dbSet = db.Get<T>();
            var listProps = DbModel.GetNestedProperties<T>()
                .Where(x => x.ParentProperty is null
                            && x.Property.PropertyType.IsGenericType
                            && x.Property.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>)
                            && x.Property.PropertyType.GenericTypeArguments[0].Implements<IModel>())
                .Select(x => x.Property)
                .ToArray();
            var existing = await dbSet.IncludeProperties(listProps.Select(x => x.Name).ToArray())
                .SingleOrDefaultAsync(findExisting, cancellationToken: cancellationToken);
            if (existing is not null)
            {
                if (model is AuditableModel auditableModel)
                    auditableModel.ModifiedUtc = DateTime.UtcNow;
                existing.Patch(model, true);
                foreach (var prop in listProps)
                {
                    var itemType = prop.PropertyType.GenericTypeArguments[0];
                    var currentList = prop.GetValue(existing);
                    var newList = prop.GetValue(model);
                    _upsertMultipleMethod.MakeGenericMethod(itemType).Invoke(null, [db, currentList, newList]);
                }
            }
            else
            {
                if (model is AuditableModel auditableModel)
                    auditableModel.CreatedUtc = DateTime.UtcNow;
                await dbSet.AddAsync(model, cancellationToken);
            }
        }

        private static readonly MethodInfo _upsertMultipleMethod =
            typeof(DbExtensions).GetMethod(nameof(UpsertMultiple), BindingFlags.Static | BindingFlags.NonPublic)!;

        private static void UpsertMultiple<T>(
            this IDb db,
            ICollection<T> currentEntities,
            ICollection<T> newEntities) where T : class, IModel
        {
            currentEntities ??= new HashSet<T>();
            newEntities ??= new HashSet<T>();
            var dbSet = db.Get<T>();
            var entitiesToRemove = currentEntities
                .Where(current => !newEntities.Contains(current));
            foreach (var entity in entitiesToRemove)
                dbSet.Remove(entity);

            foreach (var newEntity in newEntities)
            {
                var existingEntity = currentEntities.FirstOrDefault(current => current?.Equals(newEntity) ?? false);

                if (existingEntity is null)
                    dbSet.Add(newEntity);
                else
                {
                    existingEntity.Patch(newEntity, true);
                    dbSet.Update(existingEntity);
                }
            }
        }
    }
}
