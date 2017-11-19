using System;
using System.IO;
using Xunit;

namespace NShim.Tests
{
    public class FrontendTests
    {
        [Fact]
        public void SimpleShimBuilderTest()
        {
            var shim = Shim.Replace(() => DateTime.Now)
                           .With((Func<DateTime>)(() => new DateTime(2020, 1, 1)));
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);

            shim = Shim.Replace(() => DateTime.DaysInMonth(It.Any<int>(), It.Any<int>()))
                .With((Func<int, int, int>)((y, d) => 0));
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);

            shim = Shim.Replace(() => It.Any<DateTime>().AddTicks(It.Any<long>()))
                .With((FuncRef<DateTime, long, DateTime>)((ref DateTime datetime, long ticks) => new DateTime()));
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);

            shim = Shim.Replace(() => Console.WriteLine(It.Any<string>()))
                .With((Action<string>)(s => {}));
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);

            shim = Shim.Replace(() => It.Any<TextWriter>().WriteLine(It.Any<string>()))
                .With((Action<TextWriter, string>)((tw, s) => { }));
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);

            shim = Shim.Replace(() => Console.Out.WriteLine(It.Any<string>()))
                .With((Action<TextWriter, string>)((tw, s) => { }));
            Assert.Equal(Console.Out, shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);
        }

        [Fact]
        public void ReplaceTest()
        {
            var shim = Shim.For<DateTime>()
                           .WithParameters<long>()
                           .Replace(It.Any<DateTime>().AddTicks)
                           .With((ref DateTime time, long ticks) => time);
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Replacement);

            Shim.For<DateTime>()
                .WithParameters<int, int>()
                .Replace(DateTime.DaysInMonth)
                .With((ref DateTime time, int year, int month) => 0);

            Shim.For<string>()
                .WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .With((s, i) => s);

            Shim.For<string>()
                .WithParameters<string>()
                .Replace(string.Copy)
                .With((s, i) => s);

            Shim.Replace(() => It.Any<string>()[It.Any<int>()]);
            
            /*Shim.WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .On<string>() ???
                .With((s, i) => s);

            Shim.WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .On<string>()*/


            //    .Replace(Console.WriteLine)
        }
    }
}
