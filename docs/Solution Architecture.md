# Solution Architecture
SizeBench is based on the [Debug Interface Access (DIA) SDK](https://msdn.microsoft.com/en-us/library/x93ctkx8.aspx)
that ships with Visual Studio. DIA can be difficult to use due to the shape of 
the API, especially from managed code, so SizeBench builds wrappers on top of 
the core DIA library to make writing new binary analyses easier. 

The aim of the project is to produce a "pure" analysis engine that can do all 
kinds of interesting things on top of just a binary and PDB.  On top of that
analysis engine there is a GUI tool that can be used to explore a binary in a more
ad-hoc fashion, meant to be used by an individual person interested in
understanding a specific binary.  Other tools have been and will continue to be
built on top of that underlying analysis engine for broader analysis of things like
an entire SKU of a large product like Windows or Office, perhaps in an automated
fashion or as a command-line set of tools.

If you [clone the repo and open in VS](/Contributing.md), you'll see that in Visual
Studio it is separated into several Solution Folders which roughly translate to the layers
of the architecture.

## Analysis Engine
This is the fundamental purpose of SizeBench - to rip apart a binary and find
interesting things to look at.  This is the layer where DIA calls are made and
turned into a useful object model.

This is the layer where analysis code should be written, such as examining a 
binary for wasteful virtuals or duplicate static data, and so on.  That way this
information is available to any client, whether it be a GUI, command-line tool,
or whatever.

The core object created here is called a `Session` (because DIA calls it a
session - in hindsight maybe this isn't the best name).  A session can be opened
using a binary and a PDB pair that match, and then you can go spelunking around
that session for various things like all the symbols in a given section (`.text` or
`.rdata`, for example).

Within the Analysis Engine is a PE parser.  We need this because not all the
interesting parts of a binary are accessible through DIA, so SizeBench also
includes a handwritten raw PE parser that can look at any PE file for additional
data.  Some examples of things that DIA cannot see include the PDATA and XDATA
parts of a DLL, or the actual bytes contained in a piece of read-only data.  DIA
can only determine that a symbol exists in read-only data and the size and type
of it - but the actual bytes are left in the binary, which may be desirable to
look at to detect duplication, for example.

The Analysis Engine is also where all the diffing infrastructure exists, to take
two binaries/PDBs, open two `Session` objects, and then diff across them.  This involves
considerably more heuristics as matching something from one binary to something
in another is fuzzy (as one example, if a repo is cloned in two folders, building
the same code, they will have different Names for each Compiland and Lib, even
though it's the same code.  So the filenames have to be heuristically matched).

For more details, see [Analysis Engine internals](Analysis%20Engine%20Internals.md).

## Core
The "core" layer is where the base services exist that are used by both the
analysis engine and any client.  Examples include logging support and error reporting.
This layer should stay very thin over time.

## GUI
This is where the GUI comes in, as the name suggests - this is where the WPF visualizations of the
data from the Analysis Engine exist.  Individual controls, pages, dialogs, and
so on.

#### GUI\SizeBench.Packaging Project
This project packages up the GUI exe into an MSIX package so it
can be put in the Windows Store for distribution and updates.

## Tools
This folder contains a couple tools at the moment.

#### Tools\SKUCrawler
This tool can crawl an entire folder of binaries and dump all the information
into a SQLite database.  This can be useful as it allows asking a question 
like "how many binaries in this product link in foo.lib? ...and how many total
bytes does foo.lib contribute to this product as a whole?" - which can help
decide if a lib should become a dynamic library to single-instance it, for
example.

This tool is used by the Windows Engineering System.

#### Tools\BinaryBytes
This tool can similarly crawl an individual binary or folder of binaries and
dump information to SQLite.  It differs from SKUCrawler in that it also looks
for padding bytes between symbols and records additional information about
symbol inlining, which some teams find very valuable in doing perf analysis.


## PathLocators
This is where SizeBench logic is for locating a binary from a PDB path (someday
this should also be able to go the other direction).  There's currently support
for looking in just one place - local builds (pdb and dll in the same folder).
This is left as an extensibility point so in the future other lookups could be done,
such as using symbol servers.

This stuff is meant to mostly be a convenience for the GUI tool - it's likely that
command-line tools would just specify both paths.


## Tests
Includes test infrastructure code shared among many projects, the RunSettings
file that Visual Studio  (and the continuous integration system in Azure DevOps) use to
run the tests, and a couple helpful scripts and MSBuild templates for creating
new test PE files.

This is also where some commonly-used test data is, for synthetic tests that
need to run fast without opening an entire binary/PDB for real.  A large number
of tests share a common set of synthetic data that tries to be exhaustive in
scenarios.  This also avoids a lot of repetition in the unit-like tests.

For more details on testing, see [Test Strategy](Test%20Strategy.md).

## Solution Configuration
SizeBench is a 64-bit process because it's very memory hungry.  Though it
supports analyzing binaries of any CPU architecture, the tool itself is just x64.
Thus, for all the product code each project needs only one build flavor - x64.
Of course Debug and Release builds are still useful so those are supported too.