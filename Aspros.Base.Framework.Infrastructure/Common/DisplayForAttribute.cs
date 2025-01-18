namespace Aspros.Base.Framework.Infrastructure
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class DisplayForAttribute(string name) : Attribute
    {
        public string Name { get; } = name;
    }
}
