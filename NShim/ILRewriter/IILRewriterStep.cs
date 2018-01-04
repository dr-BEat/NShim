namespace NShim.ILRewriter
{
    internal interface IILRewriterStep
    {
        void Rewriter(ILProcessor processor);
    }
}
