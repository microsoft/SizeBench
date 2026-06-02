# Test Strategy
Each Project in SizeBench has a corresponding `<projectname>.Tests` test
assembly, which should thoroughly test that component in isolation in a unit
test style fashion.

Then there are integration tests which inspect a real binary to ensure end-to-end
analysis can handle the complexities of the stuff real toolchains spit out.


## Unit Tests
The unit tests are filled with mocks, stubs, fakes...whatever is needed to be
able to test the functionality in question without dependency on a real binary.
Their aim is to be fast and have high code coverage, so that they're not onerous
to run after each build in Visual Studio.


## Integration Tests
These tests are in the `*.RealPETests` projects, and they depend on "Test PEs" 
that come in two flavors:

* Binaries built in SizeBench's solution, so we can see the end-to-end flow of
the code to the compiler, linker, and how SizeBench parses the results.

* Checked-in binaries for cases we hit with real products like WinUI, 
which are so complex that generating a minimal repro Test PE inside
SizeBench isn't reasonable, but which expose important bugs or codepaths in the
Analysis Engine.  This set should stay as small as possible, we should always
favor the test PEs we can build ourselves, so we can understand the source code
that generates a given binary, and because the checked-in binaries and PDBs are
not great for git (large binary files) where at least the TestPEs built in the
repo tend to be on the smaller side.


## What Tests To Run And When
Most of the tests are quite fast - on a moderate machine in 2024, over 1000 tests can execute
even in Debug configuration in under a minute.

But some tests are quite slow, like for PGO'd binaries or disassembly of complex functions with
DbgX.  All the really slow tests are annotated this way:

`[TestCategory(CommonTestCategories.SlowTests)]`

If you use the [ExcludeSlowTests.playlist](/src/ExcludeSlowTests.playlist) playlist in the Visual
Studio Test Explorer you can have a better experience of using "run all tests" and running the vast
majority of tests for an inner loop.  You can run the slow tests from the default Test Explorer
if you really want to but they can take 10 minutes or so, so be prepared to chill.  All tests
run in the PR and CI pipelines so you can also just let those be your safety check later.


#### Updating Test PEs
Test PEs are not built every time you build in VS as they rarely change and can
be slow to build.  Also, the layout may subtly change as compiler versions get
patched and that's not interesting to keep up-to-date in the tests.
As a result, you have to manually update the Test PEs if you change the code
under "Analysis Engine\Tests for Analysis".  Once you change the test PE,
copy/paste the resulting `Foo.dll, Foo.pdb, and Foo.map` files from the 
`TestPEs_Staging` folder to `TestPEs`.

Note that this also saves off a .map file for each binary.  This is really handy
when sending out pull requests as the map file's diff is readable as text, so the
reviewer can understand what changed at the binary level to understand the
corresponding test changes, if necessary.

#### How To Add A New Test PE
The test PEs all share a lot of properties, so a common set of MSBuild properties
has been added to the solution - see `TestPEDll.props`.  When adding a new test
project (which should be under the [TestPEProjects folder](/src/TestPEProjects), 
look at how the other projects look and mimic them as much as possible.
This will require hand-editing the .vcxproj file when you add a new Test PE.

#### Solution Configuration
See [Solution Architecture](Solution%20Architecture.md) about what to maintain
for the Solution Configuration overall.  For Test PEs, it is desirable that they
exist for other CPU architectures like x86 or ARM.  Make each Test PE only one
CPU architecture, and name the project with the CPU architecture somewhere in its
name, for consistency with existing test collateral.