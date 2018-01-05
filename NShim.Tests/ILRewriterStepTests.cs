using System;
using System.Linq;
using System.Reflection.Emit;
using NShim.ILRewriter;
using NShim.ILRewriter.Steps;
using NShim.Tests.Examples;
using Xunit;

namespace NShim.Tests
{
    public class ILRewriterStepTests
    {
        [Fact]
        public void RemoveConstrainedStepTest()
        {
            var nullable = (bool?) false;
            string TestMethod(int a)
            {
                var str = nullable.ToString();
                var s = new ExampleStruct(a);
                return str + s.ToString();
            }
            Func<int, string> func = TestMethod;
            var reader = new ILReader.ILReader(func.Method);

            var instructions = reader.ToList();

            var processor = ILProcessor.FromILInstructions(instructions);

            var step = new RemoveConstrainedStep();


            Assert.Equal(2, processor.Instructions.Count(i => i.OpCode == OpCodes.Constrained));

            step.Rewriter(processor);

            Assert.DoesNotContain(OpCodes.Constrained, processor.Instructions.Select(i => i.OpCode));
        }
    }
}
