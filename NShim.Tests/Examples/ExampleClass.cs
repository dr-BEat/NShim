﻿namespace NShim.Tests.Examples
{
    public class ExampleClass
    {
        public ExampleClass(int factor)
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

        public virtual int VirtualTestMethod(int a)
        {
            return a * Factor;
        }
    }
}