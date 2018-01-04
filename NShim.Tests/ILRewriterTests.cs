using System;
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
            var rewrite = ILRewriter.ILRewriter.Rewrite(methodInfo, context);

            var result = (int)rewrite.Invoke(null, new object[] { 3, context });

            Assert.Equal(6, result);
        }

        [Fact]
        public void RewriteInstanceMethod()
        {
            var methodInfo = typeof(ExampleClass).GetMethod(nameof(ExampleClass.InstanceTestMethod), BindingFlags.Public | BindingFlags.Instance);
            var context = new ShimContext();
            var rewrite = ILRewriter.ILRewriter.Rewrite(methodInfo, context);

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
            var rewrite = ILRewriter.ILRewriter.Rewrite(methodInfo, context);

            var result = (int)rewrite.Invoke(null, new object[] { new ExampleClassChild(2), 3, context });

            Assert.Equal(12, result);
        }

        [Fact]
        public void RewriteConstructor()
        {
            var info = typeof(ExampleClass).GetConstructor(new[] { typeof(int) });
            var context = new ShimContext();
            var rewrite = ILRewriter.ILRewriter.Rewrite(info, context);

            var exampleClass = new ExampleClass(2);
            rewrite.Invoke(null, new object[] { exampleClass, 3, context });
            Assert.Equal(3, exampleClass.Factor);
            
            info = typeof(ExampleStruct).GetConstructor(new[] { typeof(int) });
            rewrite = ILRewriter.ILRewriter.Rewrite(info, new ShimContext());
            var dele = (ExampleStructConstructor)rewrite.CreateDelegate(typeof(ExampleStructConstructor));

            var exampleStruct = new ExampleStruct(2);
            dele(ref exampleStruct, 3, context);
            
            Assert.Equal(3, exampleStruct.Factor);
        }

        private delegate void ExampleStructConstructor(ref ExampleStruct @this, int factor, ShimContext context);

        [Fact]
        public void RewriteLocalMethod()
        {
            int TestMethod(int a)
            {
                return a * 2;
            }
            Func<int, int> func = TestMethod;
            var rewrite = ILRewriter.ILRewriter.Rewrite(func.Method, new ShimContext());

            var result = (int)rewrite.Invoke(null, new[] { func.Target, 3, new ShimContext() });

            Assert.Equal(6, result);
        }
    }
}
