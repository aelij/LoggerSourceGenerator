using System;

namespace Microsoft.Extensions.Logging
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class LoggerEventAttribute : Attribute
    {
        public LogLevel Level { get; set; }
        public int EventId { get; set; }
        public string? Format { get; set; }
    }
}
