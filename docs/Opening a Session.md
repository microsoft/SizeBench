# Opening a Session

A Session has to be created asynchronously because we need to set up the DIA thread to ensure we keep all DIA
calls on a background thread as they can be quite slow and DIA is thread-affinitive as a COM API.  So a Session
is created via the Session.Create static function that returns an awaitable Task.

When getting a Session ready, we need to do a bunch of things in a careful order to ensure the Session has all
the global state it will need before we give it back to a caller.  That way they can call other APIs in whatever
order they want (say, loading all the wasteful virtuals first, then all the static libraries, then all the 
symbols in one specific libarry, etc.).  Those other APIs are as lazy as they can be, doing only what they must
to satisfy the request, but to achieve that a bunch of stuff does happen eagerly before the Session object
is ever returned from Session.Create.

This whole dance happens in Session.InitializeDIAThread.  Here's what it does, in order, with an
explanation of why each step needs to happen in this order as it's pretty complicated at this point:

1. We load DIA's DLL and get the IDiaDataSource, feed it the PDB we're trying to load, and get out an
   IDiaSession.  That's DIA's core thing that we'll be querying all over the place, so it of course
   has to be first.

2. We check the MachineType to see if this is an unsupported binary so we don't bother going deeper into
   types of binaries we know we can't handle yet.  For example, this is where we reject managed (.NET)
   binaries, and binaries that .NET Framework's ngen produces which, though native, don't have symbols
   in the form we expect.  We also check the linker command-line at this point to see if it's been linked
   incrementally which is not good for static analysis due to the Incremental Linker Thunks (ILTs).  While
   we have the linker command line we also reject things linked with lld-link because the PDBs it produces
   as of now aren't complete enough for what SizeBench does.  Basically step 2 here is all about stopping
   as early as possible in a known-bad case so the stack and error message are very clear intsead of esoteric
   later down the line.

3. We check if this PDB and binary match.  PE files have a debug directory with a GUID and an "age" that
   uniquely identifies the PDB that should match.  If either the GUID or the age don't match, then likely
   this PDB is from another build of this binary, or another binary entirely.  Since we often correlate
   information between the binary and the PDB, this means we're going to have a really bad time, so we
   just early-out fail here.

4. We figure out the list of all the RVA ranges in the binary that are "virtual size-only" (they take up
   no real space on disk) because we need to know this when constructing some types of symbols and we
   could basically end up constructing any symbol at almost any time due to their recursive nature.  To
   do this we end up parsing out all the BinarySection and COFFGroup objects as that's how we walk the
   'section contributions' - and each BinarySection/COFFGroup has 'characteristics' that let us know if
   it's virtual size-only.  Note that we never need to parse any symbols when getting BinarySection,
   COFFGroup, and section contributions, so this is basically the only time we can do this before we
   enter the world of "any symbol may be needed at any time."

5. We load all the VTable public symbols, because when we're finding a vtable's data symbol (which, again,
   can happen at a bunch of places due to recursion), we may need this public symbol to determine the
   vtable data symbol's length.  Unfortunately, IDiaSymbol.length isn't meaningful for a vtable (I wish it
   were) so this is how we do it.

6. We figure out what Identical COMDAT Folding (ICF) may have done because there are lots of ways we can end
   up finding a symbol that was folded with another.  When we're looking these up, they may be data symbols,
   which is why we need the virtual size info and the vtables from steps 4 and 5 before this.  We aren't actually
   fully parsing any of these symbols yet for ICF info, just getting their SymIndexID and their name to be
   able to establish the 'canonical name' of each symbol.  This is especially important for diffing because it
   could be the case that A.dll (v1) and A.dll (v2) both contain Foo and Bar as symbols that folded together,
   but in v1 "Foo" won the name race in the linker and is the name of those bytes, and in v2 "Bar" won the name
   race and is the name of those bytes.  But for diffing purposes this isn't relevant, they're both pointing to
   "Foo & Bar" basically, so when asked what the size diff was for "Foo" we want to use Foo from v1 and Bar from
   v2, and this name canonicalization process is how we do that.

7. We parse out exception handling data from the PE file directly, since DIA does not know about these types
   of symbols.  This includes PDATA (procedure data, one entry per function that can unwind), and XDATA (exception
   data, which comes in many forms based on language and version).  The PDATA and XDATA symbols need to look up
   their 'target symbol' (which symbol they are helping to unwind) which means they may need to know about vtables
   to parse virtual functions (which in turn want to parse their parent UserDefinedType) or they may target data
   in some weird cases, which means they need to know about virtual-size-only ranges.  They also want to know
   about ICF since it's likely that a pdata/xdata symbol points to a symbol which really represents multiple names.
   So this needs to be after steps 4, 5, and 6 to be 'late enough' for all of that to be established.

8. We parse out .rsrc symbols from the binary by hand as well, since again DIA doesn't know about these symbols from
   the Win32 resource compiler (rc.exe).  This and exception handling symbols have no relationship, so this and step 7
   could happen in either order, this is just what we do arbitrarily.  Rsrc symbols don't depend on much so they could
   go earlier in this sequence just fine if we find later that we want rsrc information for one of the steps above -
   all we need to do is get past step 3 of rejecting unsupported binaries or mismatched binaries/PDBs really.

9. We're done.  At this point it should be safe to return the Session to the caller and let them do whatever they like,
   in whatever order they want.  Any SessionTask should be able to execute now with all its pre-requisite information, or
   the ability to lazy-load the parts it wants.