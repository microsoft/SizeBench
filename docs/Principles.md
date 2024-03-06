# Principles
There's a few big principles that SizeBench intends to hold to.

* Every view in the GUI is deep-linkable, so you can quickly share an analysis with
a colleague.

* Every object is built to allow quick navigation to nearby associated objects - as
an example, a binary section has a quick way to navigate to the COFF Groups within
it, and each COFF group has a quick way to navigate back to the containing binary 
section.  Think of these objects as forming a big 'web of data' inside the binary,
and following links through the binary is like following breadcrumbs on a trail - 
you might find something interesting by wandering around, but you can also quickly
go back to where you came from like browsing the web.

* Everything is rigorously tested - see [Test Strategy](Test%20Strategy.md) for
more on that.  This tool is used by many large and important teams in their
engineering systems (such as Windows and Office) so it's important that SizeBench
maintain a high level of accuracy and regression prevention.

* Everyone should be able to use SizeBench, regardless of what they
work on or what language they work in.  Initially, native binaries like C, C++,
Rust, and Zig are the target, but the goal is to deal with managed binaries, 
and so on too.

* Performance is important as this tool is run on every binary 
in Windows in every branch on every build, and we want that to be as cheap as 
possible in terms of server resources.  It's also important for an engineer at
their desk to be able to quickly navigate, to encourage spelunking and learning.

* Every situation should be well-understood and tested, or it should throw.  If
you ever reach a point where you think "I wonder how we can get here?" and there's
no answer that has an associated test, then that should be throwing an exception.
This is very helpful in learning about new situations tha may invalidate assumptions
as the tool gets run on binaries from various sources.

* Debug sanity checks are amazing - see the performance note above. If there's
an invariant you expect to hold, but which is expensive to calculate, then do
this behind an `#ifdef DEBUG` so it doesn't slow down regular folks but is
self-documenting in the codebase and would hit while running tests as we run
run tests against debug binaries in the PR and CI pipelines, and it's recommended
to run the tests in Debug when you are developing.

* No Byte Left Behind: Evenutally every byte in the binary should be completely 
understandable, so the goal is to have parsers for whatever is needed to be able
to attribute every byte to something.  This is not something that will come all
at once, but is the north star.