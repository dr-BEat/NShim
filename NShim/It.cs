using System;
using System.Runtime.Serialization;

namespace NShim
{
    public static class It
    {
        /// <summary>
        /// Returns a marker instance of type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_"></param>
        /// <returns></returns>
        public static T Any<T>()
        {
            if (typeof(T).IsValueType)
                return default(T);
            return (T)Any(typeof(T));
        }

        /// <summary>
        /// Returns a marker instance of type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object Any(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);
            var markerObject = typeof(Helper<>).MakeGenericType(type).GetField(nameof(Helper<object>.Value)).GetValue(null);
            return markerObject;
        }

        /// <summary>
        /// Creates and holds a reference to exactly one marker object of type <typeparamref name="T" /> 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private static class Helper<T> where T : class
        {
            public static readonly T Value = typeof(T)==typeof(string) ? (T)(object)new string(' ', 1) :
                                            (T)FormatterServices.GetSafeUninitializedObject(typeof(T));
        }

        /// <summary>
        /// Returns true if the given <paramref name="value"/> was produced by <see cref="Any"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="_"></param>
        /// <returns></returns>
        public static bool IsAny<T>(T value)
        {
            if (typeof(T).IsValueType)
                return true;
            return IsAny((object)value);
        }


        /// <summary>
        /// Returns true if the given <paramref name="value"/> was produced by <see cref="Any"/>.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsAny(object value)
        {
            if (value == null)
                return false;
            var type = value.GetType();
            if (type.IsValueType)
                return true;
            return ReferenceEquals(Any(type), value);
        }
    }
}
