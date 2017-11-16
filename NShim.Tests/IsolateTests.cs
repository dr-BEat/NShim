using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NShim.Tests.Examples;
using Xunit;

namespace NShim.Tests
{
    public class IsolateTests
    {
        [Fact]
        public void NopIsolateTest()
        {
            Shim.Isolate(() =>
            {
                Assert.Equal(6, ExampleClass.StaticTestMethod(3));
                var example = new ExampleClassChild(6);
                Assert.Equal(24, example.VirtualTestMethod(2));
            });
        }

        private static int ShimStaticTestMethod(int a)
        {
            return a;
        }

        [Fact]
        public void ShimStaticMethod()
        {
            Assert.Equal(6, ExampleClass.StaticTestMethod(3));
            Shim.Isolate(() =>
            {
                Assert.Equal(3, ExampleClass.StaticTestMethod(3));
            }, new Shim(typeof(ExampleClass).GetMethod(nameof(ExampleClass.StaticTestMethod), BindingFlags.Public | BindingFlags.Static),
                        null,
                        typeof(IsolateTests).GetMethod(nameof(ShimStaticTestMethod), BindingFlags.NonPublic | BindingFlags.Static)
                        ));
        }

        private static int ShimInstanceTestMethod(ExampleClass @this, int a)
        {
            Assert.NotNull(@this);
            return a;
        }

        [Fact]
        public void ShimInstanceMethod()
        {
            var shim = new Shim(
                typeof(ExampleClass).GetMethod(nameof(ExampleClass.InstanceTestMethod),
                    BindingFlags.Public | BindingFlags.Instance),
                null,
                typeof(IsolateTests).GetMethod(nameof(ShimInstanceTestMethod),
                    BindingFlags.NonPublic | BindingFlags.Static)
            );
            
            var exampleClass = new ExampleClass(2);

            Assert.Equal(6, exampleClass.InstanceTestMethod(3));
            Shim.Isolate(() =>
            {
                Assert.Equal(3, exampleClass.InstanceTestMethod(3));
            }, shim);
        }

        private static ExampleClass ShimConstructor(int a)
        {
            return new ExampleClass(a * 2);
        }

        [Fact]
        public void ShimConstructorTest()
        {
            var shim = new Shim(typeof(ExampleClass).GetConstructor(new []{typeof(int)}),
                null,
                typeof(IsolateTests).GetMethod(nameof(ShimConstructor),
                    BindingFlags.NonPublic | BindingFlags.Static)
            );
            
            Assert.Equal(2, new ExampleClass(2).Factor);
            Shim.Isolate(() =>
            {
                Assert.Equal(4, new ExampleClass(2).Factor);
            }, shim);
        }

    }
}
