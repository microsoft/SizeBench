// Turn this on if you need verbose logging to show up in the tests for DbgX failures.  It can be very noisy, though, so it's disabled by default.
//#define VERBOSE_DBGX_LOGGING

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DbgX;
using DbgX.Interfaces;
using DbgX.Interfaces.Enums;
using DbgX.Interfaces.Services;
using DbgX.Interfaces.Services.Internal;
using DbgX.Requests;
using DbgX.Requests.Initialization;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.DebuggerInterop;

internal sealed class DebuggerAdapter : IDebuggerAdapter, IDbgEnginePathCustomization
{
    private DebugEngine? _engine;
    private EngineStateController? m_stateController;

    private string _targetArch = "";

    private ulong _imageBase;

    [Conditional("VERBOSE_DBGX_LOGGING")]
    private static void VerboseTrace(string trace) => Trace.WriteLine(trace);

    #region XML data model helpers

    private static string? GetDataModelXmlField(XElement node, string fieldName)
        => node.Elements().SingleOrDefault(e => e.Attribute("Name")?.Value == fieldName)
                         ?.Attribute("DisplayValue")
                         ?.Value;

    private static bool GetBoolDataModelXmlAttribute(XElement node, string attributeName)
    {
        var modelAttribute = node.Attributes().FirstOrDefault(attr => attr.Name == attributeName);
        if (modelAttribute is null)
        {
            return false;
        }
        else
        {
            return Boolean.Parse(modelAttribute.Value);
        }
    }

    private static ulong? GetHexValueFromDataModelXmlField(XElement node, string fieldName)
    {
        var fieldAsString = GetDataModelXmlField(node, fieldName);
        if (fieldAsString is null)
        {
            return null;
        }
        else
        {
            return GetHexValue(fieldAsString);
        }
    }

    private static ulong GetHexValue(string hex)
    {
        if (hex.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
        {
            hex = hex[2..];
        }
        UInt64.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ret);
        return ret;
    }

    #endregion XML data model helpers

    public async Task OpenAsync(string binaryPath, string pdbPath, PE.MachineType machineType, CancellationToken cancellationToken, ILogger taskLog)
    {
        this._targetArch = machineType switch
        {
            PE.MachineType.I386 => "X86",
            PE.MachineType.x64 => "X64",
            PE.MachineType.ARM => "ARM",
            PE.MachineType.ARM64 => "ARM64",
            _ => throw new ArgumentOutOfRangeException(nameof(machineType), $"Unknown machine type {machineType} passed in - need to convert this to something the CreateDisassembler API will accept!"),
        };
        taskLog.Log($"Debugger target architecture is {this._targetArch}");

        // If EngHost.exe can't be found, then things just time out in ways that are really hard to debug.  So we'll try to detect it and throw
        // to make it easier to know what went wrong and not wait for long timeouts in tests.
        var engHostExePath = GetEngHostPath("amd64");
        if (!File.Exists(engHostExePath))
        {
            throw new InvalidOperationException($"EngHost.exe can't be located at {engHostExePath} - something is wrong with app or test deployment.");
        }
        else
        {
            VerboseTrace($"EngHost.exe found at {engHostExePath}");
        }

        var dbgEngDllPath = Path.Combine(GetEnginePath("amd64"), "dbgeng.dll");
        if (!File.Exists(dbgEngDllPath))
        {
            throw new InvalidOperationException($"dbgeng.dll can't be located at {dbgEngDllPath} - something is wrong with app or test deployment.");
        }
        else
        {
            VerboseTrace($"dbgeng.dll found at {dbgEngDllPath}");
        }

        var engHostProcesses = Process.GetProcessesByName("EngHost");
        VerboseTrace($"Num EngHost processes found: {engHostProcesses.Length}");

        VerboseTrace("Starting up debugging engine...");

        this._engine = new DebugEngine(this,
                                       reporter: null,
                                       logger: null,
                                       engineSettings: null,
                                       shouldPersistSettings: false,
                                       callback: CreateEngineProcess);

        this.m_stateController = new EngineStateController(this._engine);

        engHostProcesses = Process.GetProcessesByName("EngHost");

        VerboseTrace($"Num EngHost processes found: {engHostProcesses.Length}");


        VerboseTrace($"Opening dump file now..." + Environment.NewLine +
                        $"binaryPath={binaryPath}" + Environment.NewLine +
                        $"SymPath={Path.GetDirectoryName(pdbPath)}");

        var openDumpFileRequest = new OpenDumpFileRequest(binaryPath,
                                                          new EngineOptions()
                                                          {
                                                              EngineBitness = EngineArchitecture.X64,
                                                              SymPath = Path.GetDirectoryName(pdbPath),
                                                              Verbose = true
                                                          });
        await this._engine.SendRequestAsync(openDumpFileRequest, cancellationToken).ConfigureAwait(true);

        VerboseTrace("Dump file opened, waiting for initial break");
        taskLog.Log("Binary opened as dump file successfully.  Waiting for initial break.");

        await this.m_stateController!.WaitForBreakAsync().ConfigureAwait(true);

        VerboseTrace("Initial break hit - engine is ready.");
        taskLog.Log("Initial break hit - engine is ready.");

        await this._engine.SendRequestAsync(new ExecuteRequest(".lines -d", ExecuteSource.Typed)).ConfigureAwait(true); // Turn off source line fetching, we won't use it and it can cause crashes too easily on machines without access to the sources.

        var baseAddressQuery = "Debugger.State.DebuggerVariables.curprocess.Modules[0x0].BaseAddress";
        var baseAddressString = await this._engine.SendRequestAsync(new ModelQueryRequest(baseAddressQuery, completion: false)).ConfigureAwait(true);
        var doc = XDocument.Parse(baseAddressString);
        this._imageBase = GetHexValue(GetDataModelXmlField(doc.Element("Data")!, baseAddressQuery)!);

        taskLog.Log($"Debugger ImageBase for the loaded binary is 0x{this._imageBase:X}");

        await this._engine.SendRequestAsync(new ExecuteRequest(".load DbgModelApiXtn.dll", ExecuteSource.Typed)).ConfigureAwait(true);
    }

    private Process CreateEngineProcess(CreateOutOfProcessArgs args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = args.EngHostPath,
            Arguments = args.Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            ErrorDialog = false,
            LoadUserProfile = false,
        };
        return Process.Start(psi)!;
    }

    private static bool DoesFunctionContainRVA(IFunctionCodeSymbol function, uint rva)
    {
        foreach (var block in function.Blocks)
        {
            if (rva >= block.RVA && rva <= block.RVAEnd)
            {
                return true;
            }
        }

        return false;
    }

    //TODO: Consider making this a TryDisassembleAsync which returns false if an exception is thown
    //      during disassembly, so callers can decide what to do if it fails.
    public async Task<string> DisassembleAsync(IFunctionCodeSymbol function, DisassembleFunctionOptions options, ILogger taskLog, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(this.IsDisposed, GetType().Name);

        try
        {
            var sb = new StringBuilder(5000);

            var functionRvaPlusImageBase = function.PrimaryBlock.RVA + this._imageBase;
            var functionNameForDisassemblyOutput = options.ReplaceFunctionNameWith ?? function.FormattedName.IncludeParentType;
            var functionNamesThatShareAnRVAWithFunctionBeingDisassembled = new List<string>(capacity: options.FunctionsThatShareAnRVAWithDisassembledFunction.Count);
            functionNamesThatShareAnRVAWithFunctionBeingDisassembled.AddRange(from funcSharingRVA
                                                                              in options.FunctionsThatShareAnRVAWithDisassembledFunction
                                                                              select funcSharingRVA.FormattedName.IncludeParentType);

            // There is a bug/missing feature in the DbgX APIs where it won't return all the results of an especially long function - it will only return the first 100 BasicBlocks, then
            // the 101st entry in the collection will have a Name of "[...]" and a Path that is the string we can use to continue querying (it'll end in "Skip(100)" for example).  So
            // we need to do the ModelQueryRequest in a loop until we exhaust the collection without finding a "[...]" entry.

            VerboseTrace("Disassembling " + function.FullName);

            var nextQuery = $"Debugger.Utility.Code.CreateDisassembler(\"{this._targetArch}\")" +
                            $".DisassembleFunction(0x{functionRvaPlusImageBase.ToString("X", CultureInfo.InvariantCulture)})" +
                            $".BasicBlocks" +
                            $".Select(bb => new {{ StartAddress = bb.StartAddress, " +
                                                 $"EndAddress = bb.EndAddress, " +
                                                 $"Instructions = bb.Instructions.Select(i => new {{ InstructionText = i.ToDisplayString(), " +
                                                                                                   $"Address = i.Address, " +
                                                                                                   $"SourceInfo = i.SourceInformation }})" +
                                                 $"}})";

            // We'll stop if we ever iterate 101 times, that means we're probably never going to finish - no function should be >10,000 basic blocks long...I hope...
            var iterationsOfQuery = 0;
            while (iterationsOfQuery < 100)
            {
                var request = new ModelQueryRequest(nextQuery, completion: false, ModelQueryFlags.Default, recursionDepth: 5);

                var engineXml = await this._engine!.SendRequestAsync(request, token).ConfigureAwait(true);
                var rootElement = XDocument.Parse(engineXml).Root!.Elements().Single();

                if (GetBoolDataModelXmlAttribute(rootElement, "IsError"))
                {
                    throw new InvalidOperationException("Error encountered during disassembly.  Error from the debugging engine is:" + Environment.NewLine +
                                                        rootElement.Attribute("DisplayValue")?.Value);
                }

                var basicBlockElements = rootElement.Elements();

                nextQuery = null;

                foreach (var basicBlockElement in basicBlockElements)
                {
                    token.ThrowIfCancellationRequested();

                    var modelName = basicBlockElement.Attribute("Name")?.Value;
                    if (modelName == "[...]")
                    {
                        nextQuery = basicBlockElement.Attribute("Expand")?.Value;
                        break;
                    }

                    // DbgX seems to have some bugs where it can return basic blocks that are not intended to be here.
                    // But, luckily, we know the RVA Ranges of every block in this function from DIA, so we can do our own filtering here to work around these
                    // bugs for now.  So we'll check if the StartAddress of this basic block from DbgX is not contained in the function's ranges we
                    // just don't look at it and continue on, in case future blocks reported from DbgX may still apply.
                    // Note: We don't look at the DbGX BasicBlock's EndAddress since DIA and DbgX seem to sorta disagree here - DIA calls the end address "foo"
                    // but DbgX calls "the byte after the ret instruction' to be the end address, which seems to often end up as "foo+1".  Rather than
                    // hardcoding a +1 here, it seems simpler to just check the start address - if the start of the basic block is contained in our understanding
                    // if the function's ranges, the end could be off by a byte or two and that seems benign.
                    var blockStartRVA = GetHexValueFromDataModelXmlField(basicBlockElement, "StartAddress") - this._imageBase;

                    if (blockStartRVA != null)
                    {
                        if (DoesFunctionContainRVA(function, (uint)blockStartRVA))
                        {
                            await DisassembleDbgXBasicBlock(basicBlockElement, function, functionNameForDisassemblyOutput, functionNamesThatShareAnRVAWithFunctionBeingDisassembled, options, sb, token).ConfigureAwait(true);
                        }
                    }
                }

                // If we got here without finding another query to continue on with, break out of the while loop to exit disassembly.
                if (nextQuery is null)
                {
                    break;
                }
                else
                {
                    iterationsOfQuery++;
                }
            }

            return sb.ToString();
        }
        catch (OperationCanceledException)
        {
            // We want to let this continue to propagate out to callers and not log anything.
            throw;
        }
        catch (AggregateException aggEx) when (aggEx.InnerException is OperationCanceledException)
        {
            // Same as directly catching an OperationCanceledException
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types - not much else we can do, it's better than crashing.
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            taskLog.LogException("Unable to disassemble function.", ex);
            return ex.GetFormattedTextForLogging("Unable to disassemble function.  Error details:", Environment.NewLine);
        }
    }

    private async Task DisassembleDbgXBasicBlock(XElement basicBlockElement,
                                                 IFunctionCodeSymbol function,
                                                 string functionNameForDisassemblyOutput,
                                                 List<string> functionNamesThatShareAnRVAWithFunctionBeingDisassembled,
                                                 DisassembleFunctionOptions options,
                                                 StringBuilder sb,
                                                 CancellationToken token)
    {
        var blockInstructionsElement = basicBlockElement.Elements().Single(e => e.Attribute("Name")?.Value == "Instructions");
        string? nextQuery = null;
        var hasOutputSourceInfo = false;

        // We'll stop if we ever iterate 101 times, that means we're probably never going to finish - no basic block should be >10,000 instructions long...I hope...
        var iterationsOfQuery = 0;
        while (iterationsOfQuery < 100)
        {
            if (nextQuery != null)
            {
                var request = new ModelQueryRequest(nextQuery, completion: false, ModelQueryFlags.Default, recursionDepth: 5);

                var engineXml = await this._engine!.SendRequestAsync(request, token).ConfigureAwait(true);
                blockInstructionsElement = XDocument.Parse(engineXml).Root!.Elements().Single();

                if (GetBoolDataModelXmlAttribute(blockInstructionsElement, "IsError"))
                {
                    throw new InvalidOperationException("Error encountered during disassembly.  Error from the debugging engine is:" + Environment.NewLine +
                                                        blockInstructionsElement.Attribute("DisplayValue")?.Value);
                }

                nextQuery = null;
            }

            foreach (var instructionElement in blockInstructionsElement.Elements())
            {
                token.ThrowIfCancellationRequested();

                var modelName = instructionElement.Attribute("Name")?.Value;
                if (modelName == "[...]")
                {
                    nextQuery = instructionElement.Attribute("Expand")?.Value;
                    break;
                }

                if (!hasOutputSourceInfo)
                {
                    hasOutputSourceInfo = EmitSourceInfo(function, functionNameForDisassemblyOutput, functionNamesThatShareAnRVAWithFunctionBeingDisassembled, sb, instructionElement);
                }

                var instructionTextToRecord = GetDataModelXmlField(instructionElement, "InstructionText")!;

                if (options.ReplaceFunctionNameWith != null)
                {
                    instructionTextToRecord = instructionTextToRecord.Replace(function.FormattedName.IncludeParentType, options.ReplaceFunctionNameWith, StringComparison.Ordinal);
                    foreach (var functionWithSharedRVA in options.FunctionsThatShareAnRVAWithDisassembledFunction)
                    {
                        instructionTextToRecord = instructionTextToRecord.Replace(functionWithSharedRVA.FormattedName.IncludeParentType, options.ReplaceFunctionNameWith, StringComparison.Ordinal);
                    }
                }

                if (options.StripAbsoluteAddressForFunctionLocalReferences)
                {
                    var regex = new Regex(Regex.Escape(functionNameForDisassemblyOutput) + @"\+0[xX][0-9a-fA-F]+ (?<addr>\([0-9a-fA-F]+\))");
                    var mc = regex.Matches(instructionTextToRecord);
                    foreach (Match? match in mc)
                    {
                        if (match!.Success)
                        {
                            instructionTextToRecord = instructionTextToRecord.Replace(match!.Groups["addr"].Value, String.Empty, StringComparison.Ordinal);
                        }
                    }
                }

                sb.AppendLine(instructionTextToRecord);
            }

            // If we got here without finding another query to continue on with, break out of the while loop to exit disassembly.
            if (nextQuery is null)
            {
                break;
            }
            else
            {
                iterationsOfQuery++;
            }
        }
    }

    private static bool EmitSourceInfo(IFunctionCodeSymbol function,
                                       string functionNameForDisassemblyOutput,
                                       List<string> functionNamesThatShareAnRVAWithFunctionBeingDisassembled,
                                       StringBuilder sb,
                                       XElement instructionElement)
    {
        var sourceInfoElement = instructionElement.Elements().FirstOrDefault(e => e.Attribute("Name")?.Value == "SourceInfo");

        if (sourceInfoElement != null)
        {
            var functionOffsetElement = sourceInfoElement.Elements().FirstOrDefault(e => e.Attribute("Name")?.Value == "FunctionOffset");
            var offsetToPrint = String.Empty;

            if (functionOffsetElement != null)
            {
                var functionOffset = functionOffsetElement.Attribute("DisplayValue")!.Value;
                var functionOffsetAsLong = GetHexValue(functionOffset);
                if (functionOffsetAsLong == 0)
                {
                    // We leave offsetToPrint as the empty string - because it's so unlikely that it'll ever be 0, we'll assume that means we couldn't find it.
                }
                else
                {
                    offsetToPrint = $"+0x{functionOffset[2..].ToUpperInvariant()}";
                }
            }

            var functionNameElement = sourceInfoElement.Elements().FirstOrDefault(e => e.Attribute("Name")?.Value == "FunctionName");
            var functionNameToOutput = functionNameForDisassemblyOutput;

            if (functionNameElement != null)
            {
                var functionNameFromDbgX = functionNameElement.Attribute("DisplayValue")!.Value;
                // The function name can be something other than the one we're disassembling now, for example when something is inlined.
                // In this case, we want to really print out the name of the function, not our "potentially de-templatized" name.
                if (false == String.Equals(functionNameFromDbgX, function.FormattedName.IncludeParentType, StringComparison.Ordinal) &&
                    false == functionNamesThatShareAnRVAWithFunctionBeingDisassembled.Contains(functionNameFromDbgX))
                {
                    functionNameToOutput = functionNameFromDbgX;
                }
            }

            var sourceFile = GetDataModelXmlField(sourceInfoElement, "SourceFile") ?? String.Empty;
            var sourceLine = GetHexValueFromDataModelXmlField(sourceInfoElement, "SourceLine") ?? 0;

            if (sourceFile.Length > 0)
            {
                sb.AppendLine(String.Empty);
                sb.AppendLine(CultureInfo.InvariantCulture, $"{functionNameToOutput}{offsetToPrint} [{sourceFile} @ {sourceLine}]:");
                return true;
            }
        }

        return false;
    }

    #region IDbgEnginePathCustomization

    public string HomeDirectory => Environment.ExpandEnvironmentVariables(@"%TEMP%\SizeBench\Dbg");


    public string GetEngHostPath(string architecture)
        => Path.Combine(GetEnginePath(architecture), "EngHost.exe");

    public string GetEnginePath(string architecture)
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir!, architecture);
    }

    #endregion

    #region Disposal/shutdown support

    private bool IsDisposed { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (!this.IsDisposed)
        {
            this.IsDisposed = true;
            if (this.m_stateController != null)
            {
                await this.m_stateController.CleanupAsync().ConfigureAwait(true);
                this.m_stateController = null;
            }

            this._engine?.Dispose();

            Debug.Assert(this._engine?.IsShutdown() ?? true);

            this._engine = null;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }
    }

    #endregion
}
