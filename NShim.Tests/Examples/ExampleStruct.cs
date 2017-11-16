namespace NShim.Tests.Examples
{
    public struct ExampleStruct
    {
        public ExampleStruct(int factor)
        {
            Factor = factor;
        }

        public int Factor { get; }

        public static int StaticTestMethod(int a)
        {
            return a * 2;
        }

        public int InstanceTestMethod(int a)
        {
            return a * Factor;
        }
    }
}