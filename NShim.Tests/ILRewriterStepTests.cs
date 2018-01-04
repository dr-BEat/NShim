using System;
using System.Linq;
using NShim.ILRewriter;
using NShim.Tests.Examples;
using Xunit;

namespace NShim.Tests
{
    public class ILRewriterStepTests
    {
        [Fact]
        public void RemoveConstrainedStepTest()
        {
            string TestMethod(int a)
            {
                var s = new ExampleStruct(a);
                return s.ToString();
            }
            Func<int, string> func = TestMethod;
            var reader = new ILReader.ILReader(func.Method);

            var instructions = reader.ToList();

            var processor = ILProcessor.FromILInstructions(instructions);

            var step = new RemoveConstrainedStep();

            step.Rewriter(processor);
        }
    }
}
