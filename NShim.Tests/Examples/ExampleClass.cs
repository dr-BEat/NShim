namespace NShim.Tests.Examples
{
    public class ExampleClass
    {
        public ExampleClass(int factor)
        {
            Factor = factor;
        }

        public int Factor { get; set; }

        public int this[int number]
        {
            get { return number * 2; }
            set { }
        }

        public int this[int number, string test]
        {
            get { return number * 2; }
            set { }
        }

        public static int StaticTestMethod(int a)
        {
            return a * 2;
        }

        public int InstanceTestMethod(int a)
        {
            return a * Factor;
        }

        public virtual int VirtualTestMethod(int a)
        {
            return a * Factor;
        }

        public void OutTestMethod(int a, out int b)
        {
            b = a * Factor;
        }

        public void RefTestMethod(int a, ref int b)
        {
            b = a * Factor;
        }
    }
}
