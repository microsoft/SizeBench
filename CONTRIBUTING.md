# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com/microsoft/SizeBench.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Reporting Security Issues

Please do not report security vulnerabilities through public GitHub issues. Instead, please report them to the Microsoft
Security Response Center (MSRC). See [SECURITY.md](SECURITY.md) for more information.

## Adding Features or Bug Fixes

Thank you for your interest in contributing to this repository!  It's often easier if you file an issue or discussion to propose
your idea before going to the work of coding it up, so we can discuss it together first.

See below for details on how to get started building.

## Workflow

To contribute a change to the **microsoft/SizeBench** repository, please follow the high-level steps below:

1. Create a fork of the repository
1. Clone your fork locally
1. Create and push a feature branch
1. Work on your changes
1. Build and validate your changes
1. Once you are satisfied with your updates, create a new pull request

After your code has been reviewed and signed off by members of the team, the code can be merged into the main repository.

## Setting up Visual Studio
SizeBench currently supports building in Visual Studio 2022.

You'll need Visual Studio's Enterprise SKU, and you'll need the C++ and C# client development
tools, at a minimum. You'll also need to install these additional _Individual components_:

 - MSVC v140 - VS 2015 C++ build tools (v14.00) under _Compilers, build tools, and runtimes_
 - C++ Clang for Windows
 - MSBuild support for LLVM (lang-cl) toolset
 - Windows 10 SDK (10.0.15063.0) for Desktop C++ [x86 and x64]. This can be downloaded from 
   [here](https://developer.microsoft.com/en-us/windows/downloads/sdk-archive/) if it does not 
   show up in your version of the Visual Studio Installer.
 - Windows 10 SDK (10.0.20348.0). This contains the support for building the MSIX packaging project.

Once you open the SizeBench.sln Solution, you should build it once to be sure everything seems to be set
up right.  If you hit issues, file an issue and we'll help you out. Then, do the following:

1. In Visual Studio, go to the `Test -> Test Explorer` menu option.
1. Click the "Open Playlist" button and find the [ExcludeSlowTests.playlist](src/ExcludeSlowTests.playlist) file in your enlistment.
  - You can run all the tests by not selecting this playlist, but some of the PGO test are *very slow*
    so for a typical dev inner loop you want to exclude these.  They'll still run in the PR and CI
    pipelines so if you break something you'll get a signal eventually.
  - This will open a new Test Explorer window for that playlist.  The rest of the instructions apply
    to that Test Explorer instance.
1. Click on the Settings gear in Test Explorer, and select the following options:
  - Run Tests After Build
  - Configure Run Settings -> Select Solution wide runsettings File, and choose Runsettings.runsettings in the src folder
  - Processor Architecture for AnyCPU projects -> X64

Then you can Build the solution, and after the build it should run all the tests and show results in Test Explorer.

#### What to do if tests don't run in Visual Studio
There seems to be a bug in some versions of Visual Studio where not every test runs if you select "Run
All Tests" in the Test Explorer.  If you see this, try clearing out the contents of the `TestPEs_Staging`
folder and re-running the tests.

## Expectations of a change
First of all, thank you for adding a feature or fixing a bug in SizeBench!

To ensure the codebase stays maintainable and high-quality, the goal is to keep Code Coverage
at 85%+ across the Solution.  The project that's most important in this regard is the Analysis
Engine as that's the most challenging part to prove correctness of, and everything else depends
on that being correct.  So, after you complete your change, in Visual Studio just go to Test -> 
Analyze Code Coverage for All Tests.  Let it run for a couple minutes, and check the overall 
Covered Blocks %.  If it's not 85%+, definitely add tests.  Even if it is 85%+, it's strongly
encouraged to add at least one test that validates the new code you wrote.

See the [Test Strategy](docs/Test%20Strategy.md) section of the docs for more details on the
best ways to approach this.

Beyond testing, if you've added something substantial to the codebase, it'd be great if you
can also update the [Solution Architecture](docs/Solution%20Architecture.md) docs to talk 
about what you've added at a conceptual level, or add other docs to the docs folder.

If what you've added is a feature in a tool or the GUI, it'd also be awesome if you can add
documentation to the end-user docs under the [EndUserDocs](/EndUserDocs) folder. If it's a
GUI feature, even better if it can have screenshots showing off how cool it is.
