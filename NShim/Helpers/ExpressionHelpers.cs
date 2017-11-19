using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace NShim.Helpers
{
    public static class ExpressionHelpers
    {
        public static MethodBase GetMethodFromExpression(this Expression expression, out object instance)
        {
            switch (expression)
            {
                case MemberExpression memberExpression:
                {
                    if (!(memberExpression.Member is PropertyInfo propertyInfo))
                        throw new NotSupportedException("Unsupported expression");
                    instance = GetObjectInstanceFromExpression(memberExpression.Expression);
                    return propertyInfo.GetGetMethod();
                }
                case MethodCallExpression methodCallExpression:
                    instance = GetObjectInstanceFromExpression(methodCallExpression.Object);
                    return methodCallExpression.Method;
                case NewExpression newExpression:
                    instance = null;
                    return newExpression.Constructor;
                case BinaryExpression assignExpression:
                {
                    var memberExpression = (MemberExpression)assignExpression.Left;
                    if (!(memberExpression.Member is PropertyInfo propertyInfo))
                        throw new NotSupportedException("Unsupported expression");
                    instance = GetObjectInstanceFromExpression(memberExpression.Expression);
                    return propertyInfo.GetSetMethod();
                }
                default:
                    throw new NotSupportedException("Unsupported expression");
            }
        }

        public static object GetObjectInstanceFromExpression(this Expression expression)
        {
            if (!(expression is MemberExpression memberExpression))
                return null;

            object instance;
            var constantExpression = memberExpression.Expression as ConstantExpression;
            switch (memberExpression.Member)
            {
                case FieldInfo fieldInfo:
                {
                    var obj = fieldInfo.IsStatic ? null : constantExpression?.Value;
                    instance = fieldInfo.GetValue(obj);
                    break;
                }
                case PropertyInfo propertyInfo:
                {
                    var obj = propertyInfo.GetMethod.IsStatic ? null : constantExpression?.Value;
                    instance = propertyInfo.GetValue(obj);
                    break;
                }
                default:
                    throw new NotSupportedException();
            }
            EnsureInstanceNotValueType(instance);
            return instance;
        }

        private static void EnsureInstanceNotValueType(object instance)
        {
            if (instance?.GetType().IsValueType ?? false)
                throw new NotSupportedException("You cannot replace methods on specific value type instances");
        }
    }
}
