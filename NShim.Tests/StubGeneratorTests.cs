using System;
using System.Collections.Generic;
using System.Reflection;
using NShim.Tests.Examples;
using Xunit;

namespace NShim.Tests
{
    public class StubGeneratorTests
    {
        [Fact]
        public void GenerateStubTestStaticMethod()
        {
            var methodInfo = typeof(ExampleClass).GetMethod(nameof(ExampleClass.StaticTestMethod), BindingFlags.Public | BindingFlags.Static);
            var stub = StubGenerator.GenerateStubForMethod(methodInfo);

            var result = (int)stub.Invoke(null, new object[] { 3, new ShimContext() });

            Assert.Equal(6, result);
        }

        [Fact]
        public void GenerateStubTestInstanceMethod()
        {
            var methodInfo = typeof(ExampleClass).GetMethod(nameof(ExampleClass.InstanceTestMethod), BindingFlags.Public | BindingFlags.Instance);
            var stub = StubGenerator.GenerateStubForMethod(methodInfo);

            //This is not possible with the current generator
            //var result = (int)stub.Invoke(new TestClass(2), new object[] { 3, new ShimContext() });

            var result = (int)stub.Invoke(null, new object[] { new ExampleClass(2), 3, new ShimContext() });

            Assert.Equal(6, result);
        }

        [Fact]
        public void GenerateStubTestVirtualMethod()
        {
            var methodInfo = typeof(ExampleClass).GetMethod(nameof(ExampleClass.VirtualTestMethod), BindingFlags.Public | BindingFlags.Instance);
            var stub = StubGenerator.GenerateStubForVirtualMethod(methodInfo);
            
            var result = (int)stub.Invoke(null, new object[] { new ExampleClassChild(2), 3, new ShimContext() });

            Assert.Equal(12, result);
        }

        [Fact]
        public void GenerateStubTestConstructor()
        {
            var info = typeof(ExampleClass).GetConstructor(new []{typeof(int)});
            var stub = StubGenerator.GenerateStub(info);
            
            var result = (ExampleClass)stub.Invoke(null, new object[] { 3, new ShimContext() });

            Assert.NotNull(result);
            Assert.Equal(3, result.Factor);

            info = typeof(List<string>).GetConstructor(Type.EmptyTypes);
            stub = StubGenerator.GenerateStub(info);

            var resultList = (List<string>)stub.Invoke(null, new object[] { new ShimContext() });
            Assert.NotNull(resultList);
            Assert.Empty(resultList);

            info = typeof(ExampleStruct).GetConstructor(new[] { typeof(int) });
            stub = StubGenerator.GenerateStub(info);

            var resultStruct = (ExampleStruct)stub.Invoke(null, new object[] { 3, new ShimContext() });
            
            Assert.Equal(3, resultStruct.Factor);

        }

        [Fact(Skip = "Should be fixed with a better way to refer to methods")]
        public void GenerateStubTestLocalMethod()
        {
            int TestMethod(int a)
            {
                return a * 2;
            }
            var methodInfo = ((Func<int, int>) TestMethod).Method;
            var stub = StubGenerator.GenerateStubForMethod(methodInfo);

            var result = (int)stub.Invoke(null, new object[] {3, new ShimContext()});

            Assert.Equal(6, result);
        }
    }
}
