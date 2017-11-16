using System;
using System.Collections.Generic;
using System.Reflection;
using NShim.Tests.Examples;
using Xunit;

namespace NShim.Tests
{
    public class ILRewriterTests
    {
        [Fact]
        public void RewriteStaticMethod()
        {
            var methodInfo = typeof(ExampleClass).GetMethod(nameof(ExampleClass.StaticTestMethod), BindingFlags.Public | BindingFlags.Static);
            var context = new ShimContext();
            var rewrite = ILRewriter.Rewrite(methodInfo, context);

            var result = (int)rewrite.Invoke(null, new object[] { 3, context });

            Assert.Equal(6, result);
        }

        [Fact]
        public void RewriteInstanceMethod()
        {
            var methodInfo = typeof(ExampleClass).GetMethod(nameof(ExampleClass.InstanceTestMethod), BindingFlags.Public | BindingFlags.Instance);
            var context = new ShimContext();
            var rewrite = ILRewriter.Rewrite(methodInfo, context);

            //This is not possible with the current generator
            //var result = (int)rewrite.Invoke(new TestClass(2), new object[] { 3, new ShimContext() });

            var result = (int)rewrite.Invoke(null, new object[] { new ExampleClass(2), 3, context });

            Assert.Equal(6, result);
        }

        [Fact]
        public void RewriteVirtualMethod()
        {
            var methodInfo = typeof(ExampleClassChild).GetMethod(nameof(ExampleClassChild.VirtualTestMethod), BindingFlags.Public | BindingFlags.Instance);
            var context = new ShimContext();
            var rewrite = ILRewriter.Rewrite(methodInfo, context);

            var result = (int)rewrite.Invoke(null, new object[] { new ExampleClassChild(2), 3, context });

            Assert.Equal(12, result);
        }

        [Fact(Skip = "Constructors")]
        public void RewriteConstructor()
        {
            var info = typeof(ExampleClass).GetConstructor(new[] { typeof(int) });
            var context = new ShimContext();
            var rewrite = ILRewriter.Rewrite(info, context);

            var result = (ExampleClass)rewrite.Invoke(null, new object[] { new ExampleClass(2), 3, context });

            Assert.NotNull(result);
            Assert.Equal(3, result.Factor);

            info = typeof(List<string>).GetConstructor(Type.EmptyTypes);
            rewrite = ILRewriter.Rewrite(info, context);
            
            var resultList = (List<string>)rewrite.Invoke(null, new object[]{ new List<string>(), context });
            Assert.NotNull(resultList);
            Assert.Empty(resultList);

            info = typeof(ExampleStruct).GetConstructor(new[] { typeof(int) });
            rewrite = ILRewriter.Rewrite(info, new ShimContext());

            var resultStruct = (ExampleStruct)rewrite.Invoke(null, new object[] { new ExampleStruct(2), 3 });

            Assert.Equal(3, resultStruct.Factor);
        }

        [Fact(Skip = "Should be fixed with a better way to refer to methods")]
        public void RewriteLocalMethod()
        {
            int TestMethod(int a)
            {
                return a * 2;
            }
            var methodInfo = ((Func<int, int>)TestMethod).Method;
            var rewrite = ILRewriter.Rewrite(methodInfo, new ShimContext());

            var result = (int)rewrite.Invoke(null, new object[] { 3 });

            Assert.Equal(6, result);
        }
    }
}
