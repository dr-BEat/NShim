using System;
using Xunit;

namespace NShim.Tests
{
    public class FrontendTests
    {
        [Fact]
        public void ReplaceTest()
        {
            var shim = Shim.For<DateTime>()
                           .WithParameters<long>()
                           .Replace(It.Any<DateTime>().AddTicks)
                           .With((ref DateTime time, long l) => time);
            Assert.Null(shim.Instance);
            Assert.NotNull(shim.Original);
            Assert.NotNull(shim.Target);
            Assert.NotNull(shim.TargetInstance);

            Shim.For<DateTime>()
                .WithParameters<int, int>()
                .Replace(DateTime.DaysInMonth)
                .With((ref DateTime time, int i, int arg3) => 0);
            Shim.For<string>()
                .WithParameters<int>()
                .Replace(It.Any<string>().Substring)
                .With((s, i) => s);
            
            /*Shim.WithParameters<int>()
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
