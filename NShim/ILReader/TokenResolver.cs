using System;
using System.Reflection;

namespace NShim.ILReader
{
    public interface ITokenResolver
    {
        MethodBase AsMethod(int token);
        FieldInfo AsField(int token);
        Type AsType(int token);
        string AsString(int token);
        MemberInfo AsMember(int token);
        byte[] AsSignature(int token);
    }

    public class ModuleScopeTokenResolver : ITokenResolver
    {
        private readonly Module _module;
        private readonly Type[] _methodContext;
        private readonly Type[] _typeContext;

        public ModuleScopeTokenResolver(MethodBase method)
        {
            _module = method.Module;
            _methodContext = method is ConstructorInfo ? null : method.GetGenericArguments();
            _typeContext = method.DeclaringType?.GetGenericArguments();
        }

        public MethodBase AsMethod(int token)
        {
            return _module.ResolveMethod(token, _typeContext, _methodContext);
        }

        public FieldInfo AsField(int token)
        {
            return _module.ResolveField(token, _typeContext, _methodContext);
        }

        public Type AsType(int token)
        {
            return _module.ResolveType(token, _typeContext, _methodContext);
        }

        public MemberInfo AsMember(int token)
        {
            return _module.ResolveMember(token, _typeContext, _methodContext);
        }

        public string AsString(int token)
        {
            return _module.ResolveString(token);
        }

        public byte[] AsSignature(int token)
        {
            return _module.ResolveSignature(token);
        }
    }
}