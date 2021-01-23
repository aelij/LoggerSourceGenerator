using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.SourceGenerator;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SampleLogger
{
    class Program
    {
        static void Main()
        {
            var services = new ServiceCollection();
            services.AddLogging(l => l.AddConsole());
            services.AddMyLogger();
            services.AddMyInterfaceLogger();

            var sp = services.BuildServiceProvider();
            var myLogger = sp.GetRequiredService<MyLogger>();
            myLogger.AttributeModifiers(42, DayOfWeek.Wednesday);
            myLogger.CustomFormatWithSingleArgument(42);
            myLogger.NoArguments();
            myLogger.OneArgument(1);
            myLogger.TenArguments(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            myLogger.EnumerableArgument(new int?[] { 1, null, 3 }, 42, null);
            myLogger.OneArgumentAndException(2, new InvalidOperationException());
            myLogger.OneArgumentAndTwoExceptions(3, new InvalidOperationException(), new ArgumentOutOfRangeException());

            var myInterfaceLogger = sp.GetRequiredService<IMyInterfaceLogger>();
            myInterfaceLogger.InterfaceLog();

            Thread.Sleep(1000); // wait for console logger
        }
    }

    [Logger]
    partial class MyLogger
    {
        [LoggerEvent(Level = LogLevel.Warning, EventId = 3, Format = "Custom {i:x} {d} {i}")]
        public partial void AttributeModifiers(int i, DayOfWeek d);
        [LoggerEvent(Format = "Custom {i}")]
        public partial void CustomFormatWithSingleArgument(int i);
        public partial void NoArguments();
        public partial void OneArgument(int i);
        public partial void EnumerableArgument(IEnumerable<int?> i, int j, string? s);
        public partial void TenArguments(int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8, int a9, int a10);
        public partial void OneArgumentAndException(int i, InvalidOperationException ex);
        public partial void OneArgumentAndTwoExceptions(int i, InvalidOperationException exception, ArgumentException argumentException);
    }

    [Logger]
    interface IMyInterfaceLogger
    {
        void InterfaceLog();
    }
}
