// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Collections.Immutable;

namespace Microsoft.Extensions.Logging.SourceGenerator.Generator
{
    /// <summary>
    /// Formatter to convert the named format items like {NamedformatItem} to <see cref="string.Format(IFormatProvider, string, object)"/> format.
    /// </summary>
    internal static class LogValuesFormatter
    {
        private static readonly char[] FormatDelimiters = { ',', ':' };

        public static (string format, ImmutableArray<string> args, ImmutableArray<(string name, int start)> invalidArgs) MakeFormat(string format, ImmutableArray<string> args)
        {
            var foundArgs = ImmutableArray<string>.Empty;
            var invalidArgs = ImmutableArray<(string name, int start)>.Empty;

            var sb = new StringBuilder();
            int scanIndex = 0;
            int endIndex = format.Length;

            while (scanIndex < endIndex)
            {
                int openBraceIndex = FindBraceIndex(format, '{', scanIndex, endIndex);
                int closeBraceIndex = FindBraceIndex(format, '}', openBraceIndex, endIndex);

                if (closeBraceIndex == endIndex)
                {
                    sb.Append(format, scanIndex, endIndex - scanIndex);
                    scanIndex = endIndex;
                }
                else
                {
                    // Format item syntax : { index[,alignment][ :formatString] }.
                    var formatDelimiterIndex = FindIndexOfAny(format, FormatDelimiters, openBraceIndex, closeBraceIndex);
                    var argName = format.Substring(openBraceIndex + 1, formatDelimiterIndex - openBraceIndex - 1);

                    if (args.IndexOf(argName, StringComparer.OrdinalIgnoreCase) >= 0)
                    {
                        var index = foundArgs.IndexOf(argName);
                        if (index < 0)
                        {
                            index = foundArgs.Length;
                            foundArgs = foundArgs.Add(argName);
                        }

                        sb.Append(format, scanIndex, openBraceIndex - scanIndex + 1);
                        sb.Append(index.ToString(CultureInfo.InvariantCulture));
                        sb.Append(format, formatDelimiterIndex, closeBraceIndex - formatDelimiterIndex + 1);
                    }
                    else
                    {
                        invalidArgs = invalidArgs.Add((argName, openBraceIndex + 1));
                    }

                    scanIndex = closeBraceIndex + 1;
                }
            }

            return (sb.ToString(), foundArgs, invalidArgs);
        }


        private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
        {
            // Example: {{prefix{{{Argument}}}suffix}}.
            int braceIndex = endIndex;
            int scanIndex = startIndex;
            int braceOccurrenceCount = 0;

            while (scanIndex < endIndex)
            {
                if (braceOccurrenceCount > 0 && format[scanIndex] != brace)
                {
                    if (braceOccurrenceCount % 2 == 0)
                    {
                        // Even number of '{' or '}' found. Proceed search with next occurrence of '{' or '}'.
                        braceOccurrenceCount = 0;
                        braceIndex = endIndex;
                    }
                    else
                    {
                        // An unescaped '{' or '}' found.
                        break;
                    }
                }
                else if (format[scanIndex] == brace)
                {
                    if (brace == '}')
                    {
                        if (braceOccurrenceCount == 0)
                        {
                            // For '}' pick the first occurrence.
                            braceIndex = scanIndex;
                        }
                    }
                    else
                    {
                        // For '{' pick the last occurrence.
                        braceIndex = scanIndex;
                    }

                    braceOccurrenceCount++;
                }

                scanIndex++;
            }

            return braceIndex;
        }

        private static int FindIndexOfAny(string format, char[] chars, int startIndex, int endIndex)
        {
            int findIndex = format.IndexOfAny(chars, startIndex, endIndex - startIndex);
            return findIndex == -1 ? endIndex : findIndex;
        }
    }
}
