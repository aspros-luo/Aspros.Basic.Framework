namespace Aspros.Base.Framework.ClickHouse;

/// <summary>
/// Configuration for the optional ClickHouse integration.
/// ClickHouse 表结构、数据库和 SQL 仍由业务服务负责。
/// </summary>
public sealed class FrameworkClickHouseOptions
{
    /// <summary>
    /// ClickHouse.Driver connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Name of the HttpClient used by ClickHouse.Driver.
    /// </summary>
    public string HttpClientName { get; set; } = "aspros-framework-clickhouse";
}
