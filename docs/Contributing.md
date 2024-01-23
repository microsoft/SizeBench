# Contributing

## How Can I Contribute?
Just jump in!  Clone the repo, start adding stuff, send a PR :)

If you'd like more guidance, feel free to e-mail [sizebenchteam@microsoft.com](mailto:sizebenchteam@microsoft.com).

If you want to see the backlog of issues that could be looked into,
start [here](https://msblox.visualstudio.com/DefaultCollection/SizeBench/_backlogs/backlog/SizeBench%20Team/Backlog%20items)
and by all means ask questions if the backlog item isn't terribly clear.

## How To Clone And Build
##### Cloning
Go [here](https://msblox.visualstudio.com/DefaultCollection/_git/SizeBench) and click
the 'Clone' button and it'll guide you.

##### Setting up Visual Studio
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

Once you open the Solution, you should build it once to be sure everything seems to be set
up right.  If you hit issues, e-mail [sizebenchteam@microsoft.com](mailto:sizebenchteam@microsoft.com) and we'll 
help you out. Then, do the following:
```
Navigate to Test -> Test Explorer, click on the Settings gear, and select the following options:

Run Tests After Build
Configure Run Settings -> Select Solution wide runsettings File, and choose Runsettings.runsettings in the enlistment root
Processor Architecture for AnyCPU projects -> X64
```

## How To Submit A Change
We use the Pull Request workflow, pretty standard stuff.  Just send a PR
to [sizebenchteam@microsoft.com](mailto:sizebenchteam@microsoft.com).

#### Expectations of a change
First of all, thank you for adding a feature or fixing a bug in SizeBench!

To ensure the codebase stays maintainable and high-quality, the goal is to keep Code Coverage
at 85%+ across the Solution.  The project that's most important in this regard is the Analysis
Engine as that's the most challenging part to prove correctness of, and everything else depends
on that being correct.  So, after you complete your change, in Visual Studio just go to Test -> 
Analyze Code Coverage for All Tests.  Let it run for a couple minutes, and check the overall 
Covered Blocks %.  If it's not 85%+, definitely add tests.  Even if it is 85%+, it's strongly
encouraged to add at least one test that validates the new code you wrote.

See the [Test Strategy](Test%20Strategy.md) section of the docs for more details on the
best ways to approach this.

Beyond testing, if you've added something substantail to the codebase, it'd be great if you
can also update the [Solution Architecture](Solution%20Architecture.md) docs to talk 
about what you've added at a conceptual level, or add other docs to this Docs folder.

If what you've added is a feature in a tool or the GUI, it'd also be awesome if you can add
documentation to the end-user docs under the [EndUserDocs](/EndUserDocs) folder. If it's a
GUI feature, even better if it can have screenshots showing off how cool it is.