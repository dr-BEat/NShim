using System.Reflection;

namespace NShim.ILReader
{
    public interface IILProvider
    {
        byte[] GetByteArray();
    }

    public class MethodBaseILProvider : IILProvider
    {
        private readonly MethodBase _method;
        private byte[] _byteArray;

        public MethodBaseILProvider(MethodBase method)
        {
            _method = method;
        }

        public byte[] GetByteArray()
        {
            if (_byteArray == null)
            {
                var methodBody = _method.GetMethodBody();
                _byteArray = methodBody == null ? new byte[0] : methodBody.GetILAsByteArray();
            }
            return _byteArray;
        }
    }
}