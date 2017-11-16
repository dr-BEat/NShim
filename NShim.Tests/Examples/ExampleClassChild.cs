namespace NShim.Tests.Examples
{
    public class ExampleClassChild : ExampleClass
    {
        public ExampleClassChild(int factor) : base(factor)
        {
        }

        public override int VirtualTestMethod(int a)
        {
            return 2 * base.VirtualTestMethod(a);
        }
    }
}