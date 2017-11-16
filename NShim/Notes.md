Shim.WithParameter -> To avoid having to specify returns types
On<> -> to differenciate valuetypes and reference types

Context.Isolate?

Pass a single context class down the whole chain of the callstack
It contains:
- Shims
 - All shims we apply
- Cache MethodInfo -> Replacement 
 - Only replace every method once

Process
1. Replace starting method
For all method calls inside:
Is there a shim for that method?
Yes: call shim replacement
No? Replace method with stub and pass context in

Problems:
- Whole replament at once...
 - If that is not wanted then we need a stub, that replaces itself with the real method once its called.
