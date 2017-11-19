using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NShim.Helpers;

namespace NShim
{
    public partial class Shim
    {
        public static SimpleShimBuilder Replace(Expression<Action> original)
        {
            return new SimpleShimBuilder(original.Body.GetMethodFromExpression(out var instance), instance);
        }

        public static SimpleShimBuilder Replace<T>(Expression<Func<T>> original)
        {
            return new SimpleShimBuilder(original.Body.GetMethodFromExpression(out var instance), instance);
        }

        public static SimpleShimBuilder ReplaceSetter<T>(Expression<Func<T>> original)
        {
            var value = Expression.Constant(It.Any<T>(), typeof(T));
            if (original.Body is MethodCallExpression callExpression && callExpression.Method.Name == "get_Item")
            {
                //Get Matching setter method for the indexer
                var parameterTypes = callExpression.Method.GetParameters()
                                                   .Select(p => p.ParameterType)
                                                   .ToArray();
                var itemProperty = callExpression.Method.DeclaringType.GetProperty("Item", typeof(T), parameterTypes);
                
                var assign = Expression.Call(callExpression.Object, itemProperty.SetMethod, callExpression.Arguments.Concat(new [] { value }));
                return new SimpleShimBuilder(assign.GetMethodFromExpression(out var instance), instance);
            }
            else
            {
                var assign = Expression.Assign(original.Body, value);
                return new SimpleShimBuilder(assign.GetMethodFromExpression(out var instance), instance);
            }
        }
    }

    public struct SimpleShimBuilder
    {
        public MethodBase MethodBase { get; }
        public object Instance { get; }
        
        public SimpleShimBuilder(MethodBase methodBase, object instance)
        {
            MethodBase = methodBase;
            Instance = instance;
        }

        public Shim With(Delegate target)
        {
            return new Shim(MethodBase, Instance, target);
        }
    }
}
