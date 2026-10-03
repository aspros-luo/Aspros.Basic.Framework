using Aspros.Base.Framework.Application.Abstractions;

namespace Aspros.Base.Framework.Infrastructure
{
    /// <summary>
    /// Legacy compatibility contract.
    /// 新业务代码应依赖 Application.Abstractions.IWorkContext。
    /// </summary>
    public interface IWorkContext : Application.Abstractions.IWorkContext, IScoped
    {
    }
}
