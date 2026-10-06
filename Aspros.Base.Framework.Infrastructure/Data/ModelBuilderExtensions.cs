using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;
using System.Reflection;

namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
/// Convention helpers for business DbContext model configuration.
/// 业务 DbContext 的通用模型配置约定。
///
/// <para>
/// The framework owns the reflection and expression-rewriting mechanics;
/// each business service still owns its entity mapping classes and filters.
/// 框架只负责反射发现和表达式组合机制；每个业务服务仍然负责自己的实体映射类和业务过滤条件。
/// </para>
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Finds non-abstract mapping implementations in the specified assembly.
/// 查找指定程序集中的非抽象实体映射实现。
/// </summary>
    public static IEnumerable<Type> GetMappingTypes(
        this Assembly assembly,
        Type mappingInterface)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(mappingInterface);

        return GetLoadableTypes(assembly)
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                type.GetInterfaces().Any(interfaceType =>
                    interfaceType.IsGenericType &&
                    interfaceType.GetGenericTypeDefinition() == mappingInterface));
    }

    /// <summary>
    /// Applies all IEntityMappingConfiguration&lt;T&gt; implementations from an assembly.
    /// 自动应用程序集中的所有 IEntityMappingConfiguration&lt;T&gt; 实现。
    ///
    /// <para>
    /// A business DbContext can therefore keep OnModelCreating to one line while
    /// each entity retains an isolated mapping class.
    /// 业务 DbContext 可以只保留一行调用，同时让每个实体继续拥有独立的映射配置类。
    /// </para>
    /// </summary>
    public static void AddEntityConfigurationsFromAssembly(
        this ModelBuilder modelBuilder,
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var configurationType in GetMappingTypes(
                     assembly,
                     typeof(IEntityMappingConfiguration<>)))
        {
            if (Activator.CreateInstance(configurationType) is IEntityMappingConfiguration configuration)
            {
                configuration.Map(modelBuilder);
            }
        }
    }

    /// <summary>
    /// Adds a global query filter to all mapped entities assignable to T.
    /// 为所有继承或实现 T 的实体增加全局查询过滤器。
    ///
    /// <para>
    /// Existing filters are preserved and combined with AND semantics.
    /// 已有过滤器不会被覆盖，而是与新过滤器使用 AND 语义组合。
    /// </para>
    /// </summary>
    public static void AddQueryFilter<T>(
        this ModelBuilder modelBuilder,
        Expression<Func<T, bool>> expression)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(expression);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(T).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(
                entityType.ClrType,
                expression.Parameters.Single().Name);

            var newFilter = ReplacingExpressionVisitor.Replace(
                expression.Parameters.Single(),
                parameter,
                expression.Body);

            var currentFilter = entityType.GetQueryFilter();
            if (currentFilter is not null)
            {
                var currentBody = ReplacingExpressionVisitor.Replace(
                    currentFilter.Parameters.Single(),
                    parameter,
                    currentFilter.Body);

                newFilter = Expression.AndAlso(currentBody, newFilter);
            }

            entityType.SetQueryFilter(
                Expression.Lambda(newFilter, parameter));
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // Ignore types that cannot be loaded because an optional dependency
            // is unavailable. This matches the framework's resilient scanning model.
            // 某些可选依赖缺失时，忽略无法加载的类型，保持框架扫描能力的容错性。
            return exception.Types.OfType<Type>();
        }
    }
}

/// <summary>
/// Non-generic marker used by the model configuration scanner.
/// 供模型配置扫描器识别配置类的非泛型标记接口。
/// </summary>
public interface IEntityMappingConfiguration
{
    /// <summary>
    /// Applies this configuration to the EF Core model.
    /// 将当前配置应用到 EF Core 模型。
    /// </summary>
    void Map(ModelBuilder modelBuilder);
}

/// <summary>
/// Strongly typed entity mapping contract.
/// 强类型实体映射契约。
/// </summary>
public interface IEntityMappingConfiguration<TEntity> : IEntityMappingConfiguration
    where TEntity : class
{
    /// <summary>
    /// Configures one entity type.
    /// 配置单个实体类型。
    /// </summary>
    void Map(EntityTypeBuilder<TEntity> entityTypeBuilder);
}

/// <summary>
/// Convenience base class for strongly typed entity mappings.
/// 强类型实体映射的便捷基类。
///
/// <example>
/// <code>
/// public sealed class UserMap : EntityMappingConfiguration&lt;User&gt;
/// {
///     public override void Map(EntityTypeBuilder&lt;User&gt; builder)
///     {
///         builder.ToTable("user");
///         builder.HasKey(x => x.Id);
///     }
/// }
/// </code>
/// </example>
/// </summary>
public abstract class EntityMappingConfiguration<TEntity>
    : IEntityMappingConfiguration<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Configures the entity.
    /// 配置实体。
    /// </summary>
    public abstract void Map(EntityTypeBuilder<TEntity> entityTypeBuilder);

    /// <summary>
    /// Bridges the strongly typed mapping to ModelBuilder.
    /// 将强类型映射接入 ModelBuilder。
    /// </summary>
    public void Map(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        Map(modelBuilder.Entity<TEntity>());
    }
}
