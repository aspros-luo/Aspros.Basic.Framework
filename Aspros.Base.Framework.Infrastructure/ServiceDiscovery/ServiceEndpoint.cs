namespace Aspros.Base.Framework.Infrastructure;

/// <summary>
    /// Represents one concrete address returned by service discovery.
    /// 表示服务发现返回的一个具体服务实例地址。
    ///
    /// <para>
    /// <see cref="Metadata"/> keeps discovery-provider-specific information such as
    /// secure transport flags without coupling callers to Nacos types.
    /// <see cref="Metadata"/> 保存例如 secure 传输标记等服务发现元数据，
    /// 调用方不需要直接依赖 Nacos 的实例类型。
    /// </para>
    /// </summary>
    public sealed record ServiceEndpoint(
        Uri Address,
        IReadOnlyDictionary<string, string> Metadata);
