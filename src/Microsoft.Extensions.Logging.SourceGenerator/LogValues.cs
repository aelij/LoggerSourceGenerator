using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Logging.SourceGenerator
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct LogValues<T> : IReadOnlyList<KeyValuePair<string, object?>> where T : struct, ITuple
    {
        public T Values { get; }
        private readonly string[] _names;

        public LogValues(in T values, string[] names)
        {
            Values = values;
            _names = names;
        }

        public KeyValuePair<string, object?> this[int index] => new(_names[index], Values[index]);

        public int Count => _names.Length;

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var i = 0; i < _names.Length; i++)
            {
                yield return new(_names[i], Values[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class LogValues
    {
        public static LogValues<ValueTuple> Empty { get; } =
            FromTuple(ValueTuple.Create(), Array.Empty<string>());

        public static LogValues<T> FromTuple<T>(in T values, string[] names) where T : struct, ITuple =>
            new(values, names);
    }
}
