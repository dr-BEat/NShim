using System;

namespace NShim
{
    public partial class Shim
    {
		#region Actions

        public static ShimBuilder ReplaceAction(Action methodgroup)
        {
            return new ShimBuilder(methodgroup);
        }
		
		public static ShimBuilder<T1> ReplaceAction<T1>(Action<T1> methodgroup)
        {
            return new ShimBuilder<T1>(methodgroup);
        }
		
		public static ShimBuilder<T1, T2> ReplaceAction<T1, T2>(Action<T1, T2> methodgroup)
        {
            return new ShimBuilder<T1, T2>(methodgroup);
        }
		#endregion

		#region Funcs

        public static ShimBuilderFunc<T> ReplaceFunc<T>(Func<T> methodgroup)
        {
            return new ShimBuilderFunc<T>(methodgroup);
        }
		
		public static ShimBuilderFunc<T1, TResult> ReplaceFunc<T1, TResult>(Func<T1, TResult> methodgroup)
        {
            return new ShimBuilderFunc<T1, TResult>(methodgroup);
        }
		
		public static ShimBuilderFunc<T1, T2, TResult> ReplaceFunc<T1, T2, TResult>(Func<T1, T2, TResult> methodgroup)
        {
            return new ShimBuilderFunc<T1, T2, TResult>(methodgroup);
        }
		#endregion
    }

	#region Action ShimBuilder

    public struct ShimBuilder
    {
        private readonly Action _methodgroup;

        public ShimBuilder(Action methodgroup)
        {
            _methodgroup = methodgroup;
        }

        public Shim With(Action shim)
        {
            return new Shim(_methodgroup, shim);
        }
    }
	
	public struct ShimBuilder<T1>
    {
		private readonly Action<T1> _methodgroup;

        public ShimBuilder(Action<T1> methodgroup)
        {
            _methodgroup = methodgroup;
        }

        public Shim With(Action<T1> shim)
        {
            return new Shim(_methodgroup, shim);
        }
    }
	
	public struct ShimBuilder<T1, T2>
    {
		private readonly Action<T1, T2> _methodgroup;

        public ShimBuilder(Action<T1, T2> methodgroup)
        {
            _methodgroup = methodgroup;
        }

        public Shim With(Action<T1, T2> shim)
        {
            return new Shim(_methodgroup, shim);
        }
    }
	#endregion

	#region Func ShimBuilder

    public struct ShimBuilderFunc<T>
    {
		private readonly Func<T> _methodgroup;

        public ShimBuilderFunc(Func<T> methodgroup)
        {
            _methodgroup = methodgroup;
        }

        public Shim With(Func<T> shim)
        {
            return new Shim(_methodgroup, shim);
        }
    }
	
    public struct ShimBuilderFunc<T1, TResult>
    {
		private readonly Func<T1, TResult> _methodgroup;

        public ShimBuilderFunc(Func<T1, TResult> methodgroup)
        {
            _methodgroup = methodgroup;
        }

        public Shim With(Func<T1, TResult> shim)
        {
            return new Shim(_methodgroup, shim);
        }
    }
	
    public struct ShimBuilderFunc<T1, T2, TResult>
    {
		private readonly Func<T1, T2, TResult> _methodgroup;

        public ShimBuilderFunc(Func<T1, T2, TResult> methodgroup)
        {
            _methodgroup = methodgroup;
        }

        public Shim With(Func<T1, T2, TResult> shim)
        {
            return new Shim(_methodgroup, shim);
        }
    }
	#endregion
}