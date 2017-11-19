using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NShim.Helpers;

[assembly: InternalsVisibleTo("NShim.Tests")]

namespace NShim
{
    public partial class Shim
    {
        public MethodBase Original { get; }
        public object Instance { get; }
        public Delegate Replacement { get; }

        public Shim(Delegate original, Delegate replacement) : this(original.Method, It.IsAny(original.Target) ? null : original.Target, replacement)
        {
        }

        public Shim(MethodBase original, Delegate replacement) : this(original, null, replacement)
        {
        }

        public Shim(MethodBase original, object instance, Delegate replacement)
        {
            if(!ValidateReplacementMethodSignature(original, replacement.Method, out var error))
                throw new ArgumentException($"Invalid replacement method signature! Error: {error}", nameof(replacement));

            Original = original;
            Instance = instance;
            Replacement = replacement;
        }

        /// <summary>
        /// Runs the given action while applying all given shims
        /// </summary>
        /// <param name="action"></param>
        /// <param name="shims"></param>
        public static void Isolate(Action action, params Shim[] shims)
        {
            var shimContext = new ShimContext(shims);
            var rewrite = ILRewriter.Rewrite(action.Method, shimContext);
            rewrite.Invoke(null, new [] { action.Target, shimContext });
        }

        public static bool ValidateReplacementMethodSignature(MethodBase original, MethodInfo replacement, out string error)
        {
            var returnType = original.IsConstructor ? original.DeclaringType : ((MethodInfo)original).ReturnType;
            if (returnType != replacement.ReturnType)
            {
                error = $"Wrong Return type, expected {returnType} got {replacement.ReturnType}.";
                return false;
            }

            var replacementsParameters = replacement.GetParameters()
                                                    .Select(p => p.ParameterType)
                                                    .ToList();
            if (!original.IsStatic && !original.IsConstructor)
            {
                var thisType = original.DeclaringType.IsValueType
                    ? original.DeclaringType.MakeByRefType()
                    : original.DeclaringType;
                
                if (thisType != replacementsParameters.FirstOrDefault())
                {
                    error = $"Missing or wrong first parameter, expected {thisType} got {replacementsParameters.FirstOrDefault()}.";
                    return false;
                }
                replacementsParameters = replacementsParameters.Skip(1).ToList();
            }

            if (!original.GetParameters().Select(p => p.ParameterType)
                         .SequenceEqual(replacementsParameters))
            {
                var excpected = string.Join(", ", original.GetParameters().Select(p => p.ParameterType));
                var got = string.Join(", ", replacementsParameters);
                error = $"Wrong type or number of parameters, expected {excpected} got {got}.";
                return false;
            }

            error = null;
            return true;
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
            return new ShimBuilderStructFunc<T, T1, TResult>(func);
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
        private readonly Func<T1, TResult> _original;

        public ShimBuilderStructFunc(Func<T1, TResult> original)
        {
            _original = original;
        }

        public Shim With(FuncRef<T, T1, TResult> funcRef)
        {
            return new Shim(_original.Method, funcRef);
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
