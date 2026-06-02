# Analysis Engine internals

The Analysis Engine is the heart of SizeBench, so it has a pretty substantial
amount of complexity inside it.  The goal is that consumers of the engine only
be exposed to the minimal amount of complexity required.

### Session
The pivotal object from which all else springs is called a `Session`.  The name is
taken from the underlying DIA API (`IDiaSession`).

A `Session` is basically a single instance of a binary/PDB pair that is opened and
from which we can extract data.  Opening a `Session` is a very delicate dance of getting
the right things done in the right order, see [Opening a Session](Opening%20a%20Session.md)
for the full run-down of how that happens, and why it happens in that order.

`Session` is a rather overloaded object at the
moment and could do with some refactoring someday maybe, but here's what it does
today:

* Manages threading to marshal stuff onto and back from the DIA thread.

* Checks certain preconditions for a binary to be analyzable - such as that the
PDB isn't a "mini PDB" (fastlink) and that it's a supported MachineType (CPU architecture)
and that it's native code for now until someday we can support managed code.

* Reports progress on the currently executing task - the `Session` is multi-threaded
to allow UI threads to stay responsive, but ultimately it is using a queue
of operations on the all-important DIA thread - so it's doing only one thing at once,
and it reports up progress at the `Session` level, which is kinda weird for API shape
and could do with more thinking someday.

* Core APIs for consumers of the engine, like enumerating binary sections or
compilands, loading a symbol by name, and so on.

### Threading
[DIA](https://msdn.microsoft.com/library/x93ctkx8.aspx) is a
thread-affinitive COM API.  As a result, the Analysis Engine has to do some work
to marshal data between threads.  All DIA work is done on a background thread, so
that callers can let it happen asynchronously or choose to await it if they want
to.

The place this matters most is in the `Session` class, where a custom `TaskScheduler`
is used to enqueue tasks into the STA thread that DIA is opened on.

### Session Tasks
A `SessionTask` is a single unit of work that almost always executes on the DIA
thread.  These should be discrete units of work, and sort of "chunky" since they
have a decent amount of overhead in setting up logging and so on - so these aren't
meant to be very tiny things that get executed a bunch of times.

Every `SessionTask` is in a queue of work to happen on the DIA thread, so they should
take great care to be as fast as possible as they block other work from making
progress.

### Relative Virtual Addresses (RVAs), Size and VirtualSize
The relationship between these three concepts is complex and split out into its
own documentation page here:
[Size, VirtualSize, and RVAs](Size%20VirtualSize%20and%20RVAs.md).

### Session-Managed Objects
These form the core of the Object Model of the Session.  They are the core objects
returned to callers of the Analysis Engine.

##### Binary Sections and COFF Groups
A binary section is a part of the PE specification, which is a section of the 
binary with specific characteristics - like whether that section contains data,
if that data is read/write or read-only, whether the things in there are executable,
and so on.  By convention these have names like `.text` for code and `.rdata` for
read-only data.  See [this page](/EndUserDocs/binary-section.html) for more
documentation in detail.

A COFF Group is a subset of a section which has more semantic meaning for tools and
occasionally for runtime operations - as one example, the MSVC CRT has a special set
of COFF Groups it uses to determine what initializers to run at DLL init time.
Each COFF Group is contained in exactly one binary section, so the SizeBench OM
represents this by having each COFF Group point to its parent `BinarySection` and
each `BinarySection` has a list of COFF Groups contained within it.

Full details on COFF Groups that have been seen in the wild and what we understand
about their semantic meaning is on [this page](/EndUserDocs/coff-group.html).
Examples would be `.text$di` for dynamic initializers, `.text$x` for
exception-handling funclets, and so on.

##### Compilands and Libraries
A `Library` is fairly self-explanatory - it's generally a static library (`*.lib`) file
that is compiled into the binary.  Sometimes it can be a single `.obj`, I don't
really understand why, but I think DIA just chooses to represent all direct inputs
on the linker command-line as "libs".

`Compiland` objectss are basically C++ translation units.  Essentially `.obj` files are
compilands.

Note that there is at least one "special" compiland in many binaries that I
know of called `* Linker *` which the MSVC linker puts in for code it generates for
things like LTCG, which is also where the linker command-line is stored.

Much like binary sections and COFF Groups there is a strong relationship here,
which is represented in the OM.  Every `Library` has a list of `Compilands` contained
within it, and every `Compiland` points back to the `Library` it is contained in.

##### Contributions
A "contribution" is a primitive inside of a binary, which can have several
meanings.  It's basically just a collection of RVA Ranges.

An example is that a `Library` has a set of Contributions, one for each `COFFGroup`
that it contributes to.  It similarly has a set of Contributions with one for
each `BinarySection` that it contributes to.  In this way, you can see, for
example, how much of the `.text` binary section this specific `Library` has contributed.

Note that it's important to realize this is what was contributed after all
optimizations - so if `Library` "a.lib" contains a symbol that is COMDAT folded
with a symbol in another `Library` "b.lib", then a.lib will not have a contribution
containing the RVA range of that symbol - it is instead attributed to b.lib.  This
means that if you stopped linking in b.lib or those symbols stopped folding together,
a.lib may grow in contribution size without any of its input bytes actually changing.

Contributions are also done after all dead code removal (like `/OPT:REF`) so they won't
show everything from the input, because it didn't survive into the output.

Lastly, Contributions from DIA are much finer-grained, and SizeBench tries hard to
smush these together into as big of a contiguous block as it can, to minimize the number
of contributions in the object model, because many tiems we want to iterate over all the
contributions in some object and do things with them - by minimizing the set of these we
save memory and CPU cycles in lots of operations.  So there's 'coalescing' logic that looks
kinda complicated and messy, that's why it's there.

##### Symbols
A symbol is one specific thing in a binary like a function or a user-defined
data type, or a piece of data.  Lots of things in DIA are "symbols" but SizeBench
does not expose all of these as symbols - for example, Compilands are symbols
but we have a fancier `Compiland` data type in the OM because the use-cases for
compilands tend to vary wildly compared to what you generally want to do with a
`Symbol` as a caller of the engine.

Some Symbols derive from `ISymbol`, when they have an RVA range that they occupy, but
some derive from `TypeSymbol` when they represent a type which is more like a shape and
doesn't occupy bytes itself.

The other weird type of symbol is `AnnotationSymbol` which does not take up space in the
binary as annotations are only in the PDB, and which is also not a type, so it's not derived
from `ISymbol` or `TypeSymbol`.

##### Functions
Functions are the most complicated type of symbol because they can be composed of multiple blocks
that are each discontiguous in the binary.  As a result, there is a type called `IFunctionCodeSymbol`
which abstracts between the two kinds of functions - simple ones and complex ones.

`SimpleFunctionCodeSymbol` is for the kind of function that most binaries have most of the time - a
single RVA range with all the bytes of code in that function.  A simple function is both a `CodeBlockSymbol`
and its own `IFunctionCodeSymbol`.

`ComplexFunctionCodeSymbol` is, well, the complex kind that can be generated by tools like
[Profile Guided Optimization](https://docs.microsoft.com/cpp/build/profile-guided-optimizations?view=msvc-160).
These functions can have multiple blocks separated across different parts of the binary - such as a
'hot' block in one COFF Group and a 'cold' block in another COFF Group.  Complex functions have a
'primary' block which is their entry point and then one or more 'separated' blocks which are not
contiguous with the primary.  Note that multiple functions can share the same separated blocks if they
fold together - this is somewhat common if cold error handling logic is identical between multiple functions,
for example.

Functions are quite complex when dealing with diffs too, because a function could go from being simple to
being complex between versions of a binary (or vice-versa).