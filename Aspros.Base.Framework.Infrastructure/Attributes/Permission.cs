namespace Aspros.Base.Framework.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class Permission(string code) : Attribute
    {
        public string Code { get; set; } = code;
    }
}
