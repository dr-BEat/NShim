using System;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NShim.Tests")]

namespace NShim
{
    public partial class Shim
    {
        public Shim(Delegate original, Delegate target)
        {
            //Original = original;
            //Target = target;
        }

        public Shim(MethodBase original, object instance, MethodInfo target)
        {
            Original = original;
            Instance = instance;
            Target = target;
        }

        public MethodBase Original { get; }
        public object Instance { get; }
        public MethodInfo Target { get; }

        public static void Isolate(Action action, params Shim[] shims)
        {
            var shimContext = new ShimContext(shims);
            var rewrite = ILRewriter.Rewrite(action.Method, shimContext);
            rewrite.Invoke(null, new object[] { action.Target, shimContext });
        }

        public static ShimBuilder2 Replace(Delegate original)
        {
            return new ShimBuilder2(original);
        }

        public static ShimBuilderStruct<T> For<T>(RequireStruct<T> _ = null) where T : struct
        {
            return default(ShimBuilderStruct<T>);
        }
        
        public static ShimBuilderClass<T> For<T>(RequireClass<T> _ = null) where T : class
        {
            return default(ShimBuilderClass<T>);
        }

        public static ShimBuilder For<T>(T instance) where T : class
        {
            return default(ShimBuilder);
        }

        public class RequireStruct<T> where T : struct { }
        public class RequireClass<T> where T : class { }
    }

    public struct ShimBuilderStruct<T>
    {
        public ShimBuilderStruct<T, T1> WithParameters<T1>()
        {
            return default(ShimBuilderStruct<T, T1>);
        }

        public ShimBuilderStruct<T,T1,T2> WithParameters<T1, T2>()
        {
            return default(ShimBuilderStruct<T, T1, T2>);
        }
    }
    
    public struct ShimBuilderStruct<T, T1>
    {
        public Shim Replace(Action<T1> action)
        {
            return default(Shim);
        }

        public ShimBuilderStructFunc<T, T1, TResult> Replace<TResult>(Func<T1, TResult> func)
        {
            return default(ShimBuilderStructFunc<T, T1, TResult>);
        }
    }

    public struct ShimBuilderStruct<T, T1, T2>
    {
        public Shim Replace(Action<T1, T2> action)
        {
            return default(Shim);
        }

        public ShimBuilderStructFunc<T, T1, T2, TResult> Replace<TResult>(Func<T1, T2, TResult> func)
        {
            return default(ShimBuilderStructFunc<T, T1, T2, TResult>);
        }
    }

    public struct ShimBuilderStructFunc<T, T1, T2, TResult>
    {
        public Shim With(FuncRef<T, T1, T2, TResult> funcRef)
        {
            return default(Shim);
        }
    }

    public struct ShimBuilderStructFunc<T, T1, TResult>
    {
        public Shim With(FuncRef<T, T1, TResult> funcRef)
        {
            return default(Shim);
        }
    }

    public struct ShimBuilderClass<T>
    {
        public ShimBuilderClass<T, T1> WithParameters<T1>()
        {
            return default(ShimBuilderClass<T, T1>);
        }

        public ShimBuilderStruct<T, T1, T2> WithParameters<T1, T2>()
        {
            return default(ShimBuilderStruct<T, T1, T2>);
        }
    }

    public struct ShimBuilderClass<T, T1>
    {
        public Shim Replace(Action<T1> action)
        {
            return default(Shim);
        }

        public ShimBuilderClassFunc<T, T1, TResult> Replace<TResult>(Func<T1, TResult> func)
        {
            return default(ShimBuilderClassFunc<T, T1, TResult>);
        }
    }

    public struct ShimBuilderClassFunc<T, T1, TResult>
    {
        public Shim With(Func<T, T1, TResult> funcRef)
        {
            return default(Shim);
        }
    }

    /// <summary>Encapsulates a method that has three parameters and returns a value of the type specified by the <paramref name="TResult">TResult</paramref> parameter.</summary>
    /// <param name="arg1">The first parameter of the method that this delegate encapsulates.</param>
    /// <param name="arg2">The second parameter of the method that this delegate encapsulates.</param>
    /// <typeparam name="T1">The type of the first parameter of the method that this delegate encapsulates.</typeparam>
    /// <typeparam name="T2">The type of the second parameter of the method that this delegate encapsulates.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.</typeparam>
    /// <returns></returns>
    public delegate TResult FuncRef<T1, in T2, out TResult>(ref T1 arg1, T2 arg2);

    /// <summary>Encapsulates a method that has three parameters and returns a value of the type specified by the <paramref name="TResult">TResult</paramref> parameter.</summary>
    /// <param name="arg1">The first parameter of the method that this delegate encapsulates.</param>
    /// <param name="arg2">The second parameter of the method that this delegate encapsulates.</param>
    /// <param name="arg3">The third parameter of the method that this delegate encapsulates.</param>
    /// <typeparam name="T1">The type of the first parameter of the method that this delegate encapsulates.</typeparam>
    /// <typeparam name="T2">The type of the second parameter of the method that this delegate encapsulates.</typeparam>
    /// <typeparam name="T3">The type of the third parameter of the method that this delegate encapsulates.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.</typeparam>
    /// <returns></returns>
    public delegate TResult FuncRef<T1, in T2, in T3, out TResult>(ref T1 arg1, T2 arg2, T3 arg3);


    public struct ShimBuilder2
    {
        private readonly Delegate _original;

        public ShimBuilder2(Delegate original)
        {
            _original = original;
        }

        public Shim With(Delegate target)
        {
            return new Shim(_original, target);
        }
    }

    public static class Shim<T1>
    {
        public static ShimBuilder<T1> Replace(Action<T1> methodgroup)
        {
            return new ShimBuilder<T1>(methodgroup);
        }

        public static ShimBuilderFunc<T1, TResult> Replace<TResult>(Func<T1, TResult> methodgroup)
        {
            return new ShimBuilderFunc<T1, TResult>(methodgroup);
        }
    }

    public static class Shim<T1, T2>
    {
        public static ShimBuilder<T1, T2> Replace(Action<T1, T2> methodgroup)
        {
            return new ShimBuilder<T1, T2>(methodgroup);
        }

        public static ShimBuilderFunc<T1, T2, TResult> Replace<TResult>(Func<T1, T2, TResult> methodgroup)
        {
            return new ShimBuilderFunc<T1, T2, TResult>(methodgroup);
        }
    }
}
