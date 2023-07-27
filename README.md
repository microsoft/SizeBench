[![Build Status](https://dev.azure.com/ms/SizeBench/_apis/build/status/microsoft.SizeBench?branchName=main)](https://dev.azure.com/ms/SizeBench/_build/latest?definitionId=581&branchName=main)

# Welcome to the SizeBench repo

This repository contains the SizeBench tool for doing size analysis of [PE files](https://docs.microsoft.com/en-us/windows/win32/debug/pe-format) (Portable Executables such as DLL, EXE,
and SYS files) typically used on Windows.  It's intended to help with questions like:

* Why is this binary so big?
* What can be done to make it smaller?

## If you're a user of the tool
You can get help by opening SizeBench and going to Help > Show Help.  Or, in this repo the usage docs are in the [EndUserDocs](/EndUserDocs) folder.

For an introduction to this tool, see the [announcement blog post](https://devblogs.microsoft.com/performance-diagnostics/sizebench-a-new-tool-for-analyzing-windows-binary-size/)

### Quick Start

Install SizeBench [from the Microsoft Store](https://www.microsoft.com/store/productId/9NDF4N1WG7D6).

Launch it from the start menu, then select `Examine a Binary` to pick a PE file and its symbols.  Or select `Start a diff` to pick a "before" PE with its
symbols and an "after" PE with its symbols.

On the command line, use `sizebench.exe mybinary.exe mysymbols.pdb` to analyze a single binary, or `sizebench.exe ..\baseline\mybinary.exe ..\baseline\pdb\mybinary.pdb mybinary.exe .\pdb\mybinary.pdb`
to start a comparison session.

## Contributing
We are excited to work alongside you, our amazing community!

__BEFORE you start work on a feature/fix__, please read & follow our [Contributor's Guide](CONTRIBUTING.md) to help avoid any wasted or duplicate effort.

## Communicating with the Team
The easiest way to communicate with the team is via [GitHub issues](https://github.com/microsoft/SizeBench/issues/new/choose) and
[GitHub discussions](https://github.com/microsoft/SizeBench/discussions).

Please file new issues, feature requests and suggestions, but __DO search for similar open/closed pre-existing issues before creating a new issue__.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must 
follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).

Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.

## Project Road Maps

The next expected work should be roughly this:

1. Support LLD-linked binaries, to the degree they can be given LLD's PDBs.
1. Move to GitHub Actions for CI and PR pipelines.
1. Create and publish NuGet package for the Analysis Engine.
1. Support diffing of SourceFiles.
1. Improved support for binaries containing Rust code.
