using System.Runtime.Serialization;
using NShim.Helpers;

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
        public static T Any<T>(RequireStruct<T> _ = null) where T : struct
        {
            return default(T);
        }

        /// <summary>
        /// Returns a marker instance of type <typeparamref name="T" />.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="_"></param>
        /// <returns></returns>
        public static T Any<T>(RequireClass<T> _ = null) where T : class
        {
            return Helper<T>.Value;
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
        public static bool IsAny(object value)
        {
            if (value == null)
                return false;
            var type = value.GetType();
            if (type.IsValueType)
                return true;
            var markerObject = typeof(Helper<>).MakeGenericType(type).GetField(nameof(Helper<object>.Value)).GetValue(null);
            return ReferenceEquals(markerObject, value);
        }

        /// <summary>
        /// Returns true if the given <paramref name="value"/> was produced by <see cref="Any"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="_"></param>
        /// <returns></returns>
        public static bool IsAny<T>(T value, RequireStruct<T> _ = null) where T : struct 
        {
            return true;
        }

        /// <summary>
        /// Returns true if the given <paramref name="value"/> was produced by <see cref="Any"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <param name="_"></param>
        /// <returns></returns>
        public static bool IsAny<T>(T value, RequireClass<T> _ = null) where T : class
        {
            return ReferenceEquals(Helper<T>.Value, value);
        }
    }
}
