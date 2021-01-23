using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Microsoft.Extensions.Logging.SourceGenerator
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class FormatHelpers
    {
        private const string NullValue = "(null)";

        public static object FormatArgument(object? value)
        {
            if (value is null)
            {
                return NullValue;
            }

            return value;
        }

        public static string FormatArgument<T>(IEnumerable<T>? value)
        {
            if (value is null)
            {
                return NullValue;
            }

            return string.Join(", ", value.Select(o => o?.ToString() ?? NullValue));
        }
    }
}
