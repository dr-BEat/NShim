using System;
using NShim.Tests.Examples;
using Xunit;

namespace NShim.Tests
{
    public class ItTests
    {
        [Fact]
        public void Any()
        {
            Assert.True(It.IsAny(It.Any<bool>()));
            Assert.True(It.IsAny(It.Any<string>()));
            Assert.True(It.IsAny(It.Any<ExampleClass>()));

            Assert.False(It.IsAny<string>(null));
            Assert.False(It.IsAny(""));
            Assert.False(It.IsAny(" "));
            Assert.False(It.IsAny(new ExampleClass(2)));
        }

        [Fact]
        public void MethodGroupAny()
        {
            Func<int, string> group = It.Any<string>().Substring;
            Assert.NotNull(group);
        }
    }
}
