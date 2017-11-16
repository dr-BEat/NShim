using System;
using System.IO;
using Xunit;

namespace NShim.Tests
{
    public class FrontendTests
    {
        [Fact]
        public void ReplaceTest()
        {
            Shim.ReplaceAction(Console.WriteLine);
            Shim.ReplaceAction<string>(Console.WriteLine);
            Shim.ReplaceAction<string, object[]>(Console.WriteLine);

            Shim.ReplaceFunc(Console.ReadLine);
            Shim.ReplaceFunc<int, Stream>(Console.OpenStandardOutput);

            Shim<int>.Replace(Console.OpenStandardOutput)
                .With(i => null);

            Shim<int, int>.Replace(DateTime.DaysInMonth)
                .With((i, i1) => 0);

            Shim.Replace((Action<string>)Console.Out.WriteLine)
                .With((Action<TextWriter, string>)((t, s) => { }));

            Shim.Replace((Func<int, int, int>)DateTime.DaysInMonth)
                .With((FuncRef<DateTime, int, int, int>)((ref DateTime time, int i, int arg3) => Sdd(ref time, i, arg3)));

            /*Shim.For<DateTime>()
                .WithParameters<long>()
                .Replace(It.Any<DateTime>().AddTicks)
                .With((ref DateTime time, long l) => time);
            Shim.For<DateTime>()
                .WithParameters<int, int>()
                .Replace(DateTime.DaysInMonth)
                .With((ref DateTime time, int i, int arg3) => 0);
            Shim.For<string>()
                .WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .With((s, i) => s);
            Shim.WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .On<string>() ???
                .With((s, i) => s);

            Shim.WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .On<string>()*/


            //    .Replace(Console.WriteLine)
        }

        private int Sdd(ref DateTime d, int i, int i1)
        {
            return 0;
        }
    }
}
