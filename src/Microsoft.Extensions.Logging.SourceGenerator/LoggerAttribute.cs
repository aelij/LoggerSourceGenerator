using System;

namespace Microsoft.Extensions.Logging.SourceGenerator
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class LoggerAttribute : Attribute
    {
        public string? CategoryName { get; set; }
    }
}
