using System;
using System.Collections.Generic;
using System.Text;

namespace NShim
{
    public static class It
    {
        public static T Any<T>()
        {
            return default(T);
        }
    }
}
