﻿using System;
using System.IO;
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
            var shim = new Shim((Func<int, int>) ExampleClass.StaticTestMethod,
                (Func<int, int>) ShimStaticTestMethod);

            Assert.Equal(6, ExampleClass.StaticTestMethod(3));
            Shim.Isolate(() =>
            {
                Assert.Equal(3, ExampleClass.StaticTestMethod(3));
            }, shim);

            Shim.Isolate(() =>
            {
                Assert.Equal(3, ExampleClass.StaticTestMethod(3));
            }, Shim.Replace(() => ExampleClass.StaticTestMethod(It.Any<int>()))
                .With((Func<int, int>)(a => a)));
        }

        private static int ShimInstanceTestMethod(ExampleClass @this, int a)
        {
            Assert.NotNull(@this);
            return a;
        }

        [Fact]
        public void ShimInstanceMethod()
        {
            var shim = new Shim((Func<int, int>)It.Any<ExampleClass>().InstanceTestMethod,
                (Func<ExampleClass, int, int>)ShimInstanceTestMethod);
            
            var exampleClass = new ExampleClass(2);

            Assert.Equal(6, exampleClass.InstanceTestMethod(3));
            Shim.Isolate(() =>
            {
                Assert.Equal(3, exampleClass.InstanceTestMethod(3));
            }, shim);
            
            Shim.Isolate(() =>
            {
                Assert.Equal(3, exampleClass.InstanceTestMethod(3));
            }, Shim.Replace(() => It.Any<ExampleClass>().InstanceTestMethod(It.Any<int>()))
                .With((Func<ExampleClass, int, int>)((@this, a) => a)));
        }

        private static ExampleClass ShimConstructor(int a)
        {
            return new ExampleClass(a * 2);
        }

        [Fact]
        public void ShimConstructorTest()
        {
            var shim = new Shim(typeof(ExampleClass).GetConstructor(new []{typeof(int)}),
                (Func<int, ExampleClass>)ShimConstructor);
            
            Assert.Equal(2, new ExampleClass(2).Factor);
            Shim.Isolate(() =>
            {
                Assert.Equal(4, new ExampleClass(2).Factor);
            }, shim);

            Shim.Isolate(() =>
            {
                Assert.Equal(4, new ExampleClass(2).Factor);
            }, Shim.Replace(() => new ExampleClass(It.Any<int>()))
                .With((Func<int, ExampleClass>)(a => new ExampleClass(a * 2))));
        }

        [Fact]
        public void ShimPropertyGetterTest()
        {
            var exampleClass = new ExampleClass(2);
            Assert.Equal(2, exampleClass.Factor);
            Shim.Isolate(() =>
            {
                Assert.Equal(1, exampleClass.Factor);
            }, Shim.Replace(() => It.Any<ExampleClass>().Factor)
                .With((Func<ExampleClass, int>)(@this => 1)));
        }

        [Fact]
        public void ShimPropertySetterTest()
        {
            var exampleClass = new ExampleClass(3)
            {
                Factor = 2
            };
            Assert.Equal(2, exampleClass.Factor);
            Shim.Isolate(() =>
            {
                exampleClass.Factor = 5;
                Assert.Equal(2, exampleClass.Factor);
            }, Shim.ReplaceSetter(() => It.Any<ExampleClass>().Factor)
                .With((Action<ExampleClass, int>)((@this, value) => { })));
        }

        [Fact]
        public void ShimIndexerGetterTest()
        {
            var exampleClass = new ExampleClass(2);
            Assert.Equal(6, exampleClass[3]);
            Shim.Isolate(() =>
            {
                Assert.Equal(3, exampleClass[3]);
            }, Shim.Replace(() => It.Any<ExampleClass>()[It.Any<int>()])
                .With((Func<ExampleClass, int, int>)((@this, a) => a)));
        }

        [Fact]
        public void ShimIndexerSetterTest()
        {
            var exampleClass = new ExampleClass(2);
            var called = false;
            Shim.Isolate(() =>
            {
                Assert.False(called);
                exampleClass[3] = 1;
                Assert.True(called);
            }, Shim.ReplaceSetter(() => It.Any<ExampleClass>()[It.Any<int>()])
                .With((Action<ExampleClass, int, int>)((@this, index, value) => { called = true; })));
        }

        [Fact]
        public void ShimIndexerSetterTest2()
        {
            var exampleClass = new ExampleClass(2);
            var called = false;
            Shim.Isolate(() =>
            {
                Assert.False(called);
                exampleClass[3, ""] = 1;
                Assert.True(called);
            }, Shim.ReplaceSetter(() => It.Any<ExampleClass>()[It.Any<int>(), It.Any<string>()])
                .With((Action<ExampleClass, int, string, int>)((@this, index, str, value) => { called = true; })));
        }

        private delegate void OutTestFunc(int a, out int b);
        private delegate void OutTestReplacementFunc(ExampleClass @this, int a, out int b);
        [Fact]
        public void ShimOutParamMethod()
        {
            var shim = new Shim((OutTestFunc)It.Any<ExampleClass>().OutTestMethod,
                (OutTestReplacementFunc)((ExampleClass @this, int a, out int b) => b = a));

            var exampleClass = new ExampleClass(2);
            
            exampleClass.OutTestMethod(3, out var result);
            Assert.Equal(6, result);
            Shim.Isolate(() =>
            {
                exampleClass.OutTestMethod(3, out result);
                Assert.Equal(3, result);
            }, shim);
        }

        [Fact]
        public void ShimSubFunctionCallTest()
        {
            var called = false;
            Shim.Isolate(() =>
            {
                Assert.False(called);
                Console.Out.WriteLine("Test");
                Assert.True(called);
            }, Shim.Replace(() => It.Any<TextWriter>().WriteLine(It.Any<string>()))
                .With((Action<TextWriter, string>)((@this, str) => { called = true; })));
        }

        [Fact]
        public void ShimMethodOnNullableTest()
        {
            var nullable = (bool?)false;
            Shim.Isolate(() =>
            {
                //This creates a constrained opcode followed by a callvirt
                //We have to handle this specially to avoid invalid il
                nullable.ToString();
            });
        }
    }
}
