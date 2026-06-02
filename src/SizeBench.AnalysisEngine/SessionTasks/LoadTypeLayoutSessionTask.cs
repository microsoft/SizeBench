using System.Diagnostics;
using SizeBench.AnalysisEngine.Symbols;
using SizeBench.Logging;

namespace SizeBench.AnalysisEngine.SessionTasks;

internal class LoadTypeLayoutSessionTask : SessionTask<List<TypeLayoutItem>>
{
    // This task can be initiated using a type name string, which can include wildcards to load a small set of types (like "MyType*Suffix")
    // Or it can be initiated with an existing TypeSymbol that we've already loaded
    // We'll always check the TypeSymbol first, and fall back to the typename string if no TypeSymbol was provided.
    // If neither of these is specified (both are null) we'll load all types in the binary, which can be a lot of types.
    internal readonly UserDefinedTypeSymbol? TypeSymbol;
    private readonly string? TypeName;
    private readonly uint taskWideBaseOffset;

    public LoadTypeLayoutSessionTask(SessionTaskParameters parameters,
                                     string? typeName,
                                     TypeSymbol? typeSymbol,
                                     uint baseOffset,
                                     IProgress<SessionTaskProgress>? progress,
                                     CancellationToken token)
                                     : base(parameters, progress, token)
    {
        this.TaskName = $"Load Type Layout(s) for {typeSymbol?.Name ?? typeName ?? "All Types"}";
        this.TypeName = typeName;
        this.taskWideBaseOffset = baseOffset;

        // We can only load UserDefinedTypeSymbol layouts so far, but if someone asks for the layout of a pointerType or
        // a modifiedType they surely want the UDT underneath those.  So we'll 'chase through' these modification types
        // and only throw if this thing really isn't a UDT (a BasicType or a FunctionType, for example).

        if (typeSymbol != null)
        {
            if (!typeSymbol.CanLoadLayout)
            {
                throw new ArgumentException("It should be impossible to get here with a TypeSymbol that doesn't support loading a layout.");
            }

            this.TypeSymbol = ChaseThroughToUDT(typeSymbol);
            if (this.TypeSymbol is null)
            {
                throw new ArgumentException("The type symbol provided is not a UserDefinedType nor an indirect version of one (a modified type, " +
                                            "pointer type, etc. with a UDT beneath them).  Currently we don't know how to load Type Layouts for " +
                                            "these kinds of types - is this a bug in the caller, or do we need to support this now in the " +
                                            "AnalysisEngine?");
            }
        }
    }

    private static UserDefinedTypeSymbol? ChaseThroughToUDT(TypeSymbol typeSymbol)
    {
        if (typeSymbol is PointerTypeSymbol ptrType)
        {
            return ChaseThroughToUDT(ptrType.PointerTargetType);
        }
        else if (typeSymbol is ModifiedTypeSymbol modType)
        {
            return ChaseThroughToUDT(modType.UnmodifiedTypeSymbol);
        }
        else if (typeSymbol is ArrayTypeSymbol arrType)
        {
            return ChaseThroughToUDT(arrType.ElementType);
        }
        else if (typeSymbol is UserDefinedTypeSymbol udt)
        {
            return udt;
        }

        return null;
    }

    protected override List<TypeLayoutItem> ExecuteCore(ILogger logger)
    {
        this.CancellationToken.ThrowIfCancellationRequested();

        if (this.TypeSymbol != null)
        {
            this.TypeSymbol.LoadBaseTypes(this.DataCache, this.DIAAdapter, this.CancellationToken);
            return new List<TypeLayoutItem>() { LoadSingleTypeLayout(this.TypeSymbol, this.taskWideBaseOffset, logger) };
        }
        else if (this.TypeName is null)
        {
            return LoadAllTypeLayouts(logger);
        }
        else
        {
            return LoadTypeLayoutsByName(this.TypeName, logger);
        }
    }

    private List<TypeLayoutItem> LoadAllTypeLayouts(ILogger logger)
    {
        ReportProgress("Discovering all user-defined types in the binary", 0, null);

        var udts = this.DIAAdapter.FindAllUserDefinedTypes(logger, this.CancellationToken).ToList();

        using (logger.StartTaskLog("Loading all base types"))
        {
            udts.LoadAllBaseTypes(this.DataCache, this.DIAAdapter, this.CancellationToken, ReportProgress);
        }

        this.CancellationToken.ThrowIfCancellationRequested();

        return LoadTypeLayoutsFromList(udts, logger);
    }

    private List<TypeLayoutItem> LoadTypeLayoutsByName(string typeName, ILogger logger)
    {
        ReportProgress($"Discovering necessary types to load class layout for {typeName}", 0, null);

        var udts = this.DIAAdapter.FindUserDefinedTypesByName(logger, typeName, this.CancellationToken).ToList();

        using (logger.StartTaskLog("Loading all base types"))
        {
            udts.LoadAllBaseTypes(this.DataCache, this.DIAAdapter, this.CancellationToken, ReportProgress);
        }

        return LoadTypeLayoutsFromList(udts, logger);
    }

    private List<TypeLayoutItem> LoadTypeLayoutsFromList(List<UserDefinedTypeSymbol> udts, ILogger logger)
    {
        var TypeLayouts = new List<TypeLayoutItem>(capacity: udts.Count);

        const int loggerOutputVelocity = 100;
        uint nextLoggerOutput = loggerOutputVelocity;
        var symbolsEnumerated = 0;
        foreach (var udt in udts)
        {
            symbolsEnumerated++;
            if (symbolsEnumerated >= nextLoggerOutput)
            {
                ReportProgress($"Enumerated type layouts for {symbolsEnumerated}/{udts.Count} user-defined types.", nextLoggerOutput, (uint)udts.Count);
                nextLoggerOutput += loggerOutputVelocity;
            }

            this.CancellationToken.ThrowIfCancellationRequested();

            TypeLayouts.Add(LoadSingleTypeLayout(udt, baseOffset: this.taskWideBaseOffset, logger: logger));
        }

        ReportProgress($"Enumerated type layouts for {symbolsEnumerated}/{udts.Count} user-defined types.", nextLoggerOutput, (uint)udts.Count);

        TypeLayouts.Sort((tl1, tl2) => String.CompareOrdinal(tl1.UserDefinedType.Name, tl2.UserDefinedType.Name));

        return TypeLayouts;
    }

    private TypeLayoutItem LoadSingleTypeLayout(UserDefinedTypeSymbol udt, uint baseOffset, ILogger logger)
    {
        try
        {
            decimal alignmentWasteExclusive = 0;
            var dataMembers = CollectDataMembers(udt, baseOffset);

            uint usedForVFPtrsExclusive = 0;

            var baseTypeLayouts = CollectBaseTypeLayouts(udt, baseOffset, logger);
            var memberLayouts = new List<TypeLayoutItemMember>();

            // As we walk through the members we need to keep track of the one we find with the greatest
            // offset, since we'll need to know that to calculate where alignment members are.
            decimal? lastSeenOffset = null;
            var lastSeenOffsetThatsNotABitfield = baseOffset;
            decimal? lastSeenSize = null;
            uint lastSeenNonBitfieldSize = 0;
            decimal maxOffsetSeen = baseOffset;
            decimal maxOffsetPlusSizeSeen = baseOffset;

            var vfptrMembers = CreateVFPtrMembersForThisTypeIfItsNotAlreadyAccountedForInBaseType(udt, baseTypeLayouts, baseOffset);
            if (vfptrMembers != null)
            {
                memberLayouts.AddRange(vfptrMembers);
                usedForVFPtrsExclusive = (uint)vfptrMembers.Length * this.Session.BytesPerWord;
                // When calculating the last seen offset, subtract 1 because the first vfptr is at offset 0, so the second vfptr (if there is one) would
                // be the first one that goes beyond baseOffset.
                lastSeenOffset = baseOffset + (uint)((vfptrMembers.Length - 1) * this.Session.BytesPerWord);
                lastSeenOffsetThatsNotABitfield = (uint)lastSeenOffset;
                lastSeenSize = this.Session.BytesPerWord;
                lastSeenNonBitfieldSize = (uint)lastSeenSize;
                maxOffsetSeen = lastSeenOffset.Value;
                maxOffsetPlusSizeSeen = maxOffsetSeen + lastSeenSize.Value;
            }

            //TODO (Product Backglog Item 1500): write tests that use alignas(X) on a base type but not the derived type (or differ between base
            //                   and derived) to verify we calculate alignment padding correctly in these cases.
            //TODO (Product Backlog Item 1500): write tests that apply alignas(X) to a *member* of a type to verify we calculate things correctly.
            var lastMemberByOffset = GetLastMemberOffsetFromBaseTypeLayouts(baseTypeLayouts);

            if (lastMemberByOffset != null)
            {
                lastSeenOffset = lastMemberByOffset.Offset + lastMemberByOffset.Size;
                Debug.Assert(lastSeenOffset == (uint)lastSeenOffset);
                lastSeenOffsetThatsNotABitfield = (uint)lastSeenOffset;
                lastSeenSize = 0;
                lastSeenNonBitfieldSize = 0;
                maxOffsetSeen = lastSeenOffset.Value;
                maxOffsetPlusSizeSeen = maxOffsetSeen;
            }

            foreach (var member in dataMembers.OrderBy(x => x.Offset))
            {
                if (member.Offset > maxOffsetPlusSizeSeen)
                {
                    var alignmentAmount = member.Offset - maxOffsetPlusSizeSeen;

                    // The fallback to 0 is for the unusual case where the very first member is alignment padding - this can happen if someone manually
                    // inserts a padding bitfield for some reason (see TypeWithPaddingAsFirstMember in the tests).
                    var alignmentMemberOffset = (lastSeenOffset != null && lastSeenSize != null ? lastSeenOffset.Value + lastSeenSize.Value : 0);

                    var isBitFieldAlignment = (alignmentMemberOffset != (int)alignmentMemberOffset || member.Offset != (int)member.Offset);
                    var bitStartPosition = isBitFieldAlignment ? (uint)((alignmentMemberOffset - lastSeenOffsetThatsNotABitfield - lastSeenNonBitfieldSize) / 0.125m) : 0;

                    memberLayouts.Add(TypeLayoutItemMember.CreateAlignmentMember(alignmentAmount,
                                                                                 alignmentMemberOffset,
                                                                                 isBitFieldAlignment,
                                                                                 (ushort)bitStartPosition,
                                                                                 isTailSlop: false));
                    alignmentWasteExclusive += alignmentAmount;
                }

                memberLayouts.Add(member);

                // Check that the offsets don't match, because pure virtual interfaces (or base types with no data) can have the same vfptr address
                // as the next one in line.  We only want to count amount spent in actual "this" space.
                // Note we can skip the more expensive string comparison by first checking that this member meets some obvious preconditions of being
                // a vfptr: that it is not a bitfield, and that its size is equal to BytesPerWord.  Perhaps we should also check if member.Type is
                // a PointerTypeSymbol but I haven't verified that's always correct yet and as this is just an optimization, it's left out of the
                // early-outs for now.
                var isvfptr = !member.IsBitField &&
                                member.Size == this.Session.BytesPerWord &&
                                member.Name.Contains("`vfptr'", StringComparison.Ordinal);
                if (lastSeenOffset.HasValue && member.Offset != lastSeenOffset && isvfptr)
                {
                    usedForVFPtrsExclusive += this.Session.BytesPerWord;
                }
                else if (!lastSeenOffset.HasValue && isvfptr)
                {
                    usedForVFPtrsExclusive += this.Session.BytesPerWord;
                }

                lastSeenOffset = member.Offset;
                lastSeenSize = member.Size;
                maxOffsetSeen = Math.Max(maxOffsetSeen, lastSeenOffset.Value);
                maxOffsetPlusSizeSeen = Math.Max(maxOffsetPlusSizeSeen, lastSeenOffset.Value + lastSeenSize.Value);
                if (!member.IsBitField)
                {
                    lastSeenOffsetThatsNotABitfield = (uint)member.Offset;
                    lastSeenNonBitfieldSize = (uint)member.Size;
                }
                if (member.BitStartPosition == 0)
                {
                    lastSeenOffsetThatsNotABitfield = (uint)member.Offset;
                    lastSeenNonBitfieldSize = 0;
                }
            }

            // If we didn't end with as many bytes as the UDT says it has, there's padding here too in the form of tail slop.
            if (maxOffsetPlusSizeSeen < (udt.InstanceSize + baseOffset))
            {
                var alignmentAmount = baseOffset + udt.InstanceSize - maxOffsetPlusSizeSeen;
                Debug.Assert(alignmentAmount > 0);

                var alignmentMemberOffset = maxOffsetPlusSizeSeen;

                var isBitFieldAlignment = (alignmentAmount != (int)alignmentAmount);
                var bitStartPosition = isBitFieldAlignment ? (uint)((alignmentMemberOffset - maxOffsetSeen) / 0.125m) : 0;

                //TODO (Product Backlog Item 1500): write tests that use alignas(X) and see if we detect the size of this correctly.
                memberLayouts.Add(TypeLayoutItemMember.CreateAlignmentMember(alignmentAmount,
                                                                             alignmentMemberOffset,
                                                                             isBitFieldAlignment,
                                                                             (ushort)bitStartPosition,
                                                                             isTailSlop: true));
                alignmentWasteExclusive += alignmentAmount;
            }

            // We should have a member whose offset+size == (size of the whole UDT) or else we've not "filled up" the type - that will cause
            // problems when we start looking at derived types so let's verify we really attributed all the space in the UDT to the layout.
            if (// We have any members at all (otherwise this sanity check occurs in base classes or is not needed)
                memberLayouts.Count > 0 &&
                // We don't have any members that add up to the 'end size' of the type
                memberLayouts.Max(m => m.Offset + m.Size) != (udt.InstanceSize + baseOffset) &&
                // No base type has any members that add up to the 'end size' - it's possible a base type has a field that goes further than this type (see xstack<int> in the tests as an example)
                MaxOffsetPlusSizeSeenByAnyBaseType(baseTypeLayouts) != (udt.InstanceSize + baseOffset)
                )
            {
                // With ODR violations in so many binaries, it's really tough to enforce strict equality here, much though I want to for pure correctness.  Instead, if the type's
                // members add up to *at least* the size of the type, we'll let that through for Release builds.  Debug builds will still throw if things aren't exact equality
                // to continue trying to debug a better solution here that verifies types aren't too big or too small (the goldilocks of type layouts).
#if !DEBUG
                    if (memberLayouts.Max(m => m.Offset + m.Size) <= (udt.InstanceSize + baseOffset) &&
                        MaxOffsetPlusSizeSeenByAnyBaseType(baseTypeLayouts) <= (udt.InstanceSize + baseOffset))
#endif
                throw new InvalidOperationException($"We failed to attribute all the size of the UDT ({udt.Name}) to members in the layout.  This is a bug in SizeBench and should be fixed.");
            }

            return new TypeLayoutItem(udt,
                                      alignmentWasteExclusive,
                                      usedForVFPtrsExclusive,
                                      baseTypeLayouts,
                                      memberLayouts.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogException($"Failed to load type layout for {udt.Name} with baseOffset {baseOffset}", ex);
            throw new InvalidOperationException($"Failed to load type layout for {udt.Name} with baseOffset {baseOffset}", ex);
        }
    }

    private TypeLayoutItemMember[]? CreateVFPtrMembersForThisTypeIfItsNotAlreadyAccountedForInBaseType(UserDefinedTypeSymbol udt, TypeLayoutItem[]? baseTypeLayouts, uint baseOffset)
    {
        // Only add a vfptr member if one of the base types didn't already add it at this offset
        if (udt.VTableCount > 0 && !BaseTypeLayoutsContainVfptrAtOffsetAlready(udt, baseTypeLayouts, baseOffset))
        {
            var vfptrMembers = new TypeLayoutItemMember[udt.VTableCount];
            //TODO: figure out how to test this - the case that hits >1 VTableCount is "Private::XamlRuntimeType" in Windows.UI.Xaml.dll, but I have no idea why that has >1 vtable from looking at the code.
            for (var i = 0; i < udt.VTableCount; i++)
            {
                vfptrMembers[i] = TypeLayoutItemMember.CreateVfptrMember(baseOffset + (uint)(i * this.Session.BytesPerWord), this.Session.BytesPerWord);
            }

            return vfptrMembers;
        }

        return null;
    }

    private static decimal MaxOffsetPlusSizeSeenByAnyBaseType(IEnumerable<TypeLayoutItem>? baseTypeLayouts)
    {
        if (baseTypeLayouts is null)
        {
            return 0;
        }

        decimal maxToReturn = 0;

        foreach (var baseType in baseTypeLayouts)
        {
            var maxMemberInThisType = baseType.MemberLayouts != null && baseType.MemberLayouts.Count > 0 ? baseType.MemberLayouts.Max(m => m.Offset + m.Size) : 0;
            var maxMemberInAnyBaseTypeOfThisType = MaxOffsetPlusSizeSeenByAnyBaseType(baseType.BaseTypeLayouts);
            maxToReturn = Math.Max(maxToReturn, Math.Max(maxMemberInThisType, maxMemberInAnyBaseTypeOfThisType));
        }

        return maxToReturn;
    }

    private static TypeLayoutItemMember[] CollectDataMembers(UserDefinedTypeSymbol udt, uint baseOffset)
    {
        // The manual loops here are because this is called very frequently (tens to hundreds of thousands of times when loading all types in a large
        // binary).  So using simple LINQ stuff creates a huge number of allocations, thus the old-school for looping.
        var countOfNonStaticMembers = 0;
        for (var i = 0; i < udt.DataMembers.Length; i++)
        {
            if (udt.DataMembers[i].IsStaticMember == false)
            {
                countOfNonStaticMembers++;
            }
        }

        var members = new TypeLayoutItemMember[countOfNonStaticMembers];
        var memberIndex = 0;

        for (var i = 0; i < udt.DataMembers.Length; i++)
        {
            if (udt.DataMembers[i].IsStaticMember == false)
            {
                members[memberIndex] = TypeLayoutItemMember.FromDataSymbol(udt.DataMembers[i], baseOffset);
                memberIndex++;
            }
        }

        return members;
    }

    private TypeLayoutItem[]? CollectBaseTypeLayouts(UserDefinedTypeSymbol udt, uint baseOffset, ILogger logger)
    {
        if (udt.BaseTypes is null)
        {
            return null;
        }

        var baseTypeLayouts = new TypeLayoutItem[udt.BaseTypes.Count];

        uint baseTypeIndex = 0;
        foreach (var baseTypeAndOffset in udt.BaseTypes)
        {
            baseTypeLayouts[baseTypeIndex] = LoadSingleTypeLayout(baseTypeAndOffset._baseTypeSymbol, baseTypeAndOffset._offset + baseOffset, logger);
            baseTypeIndex++;
        }

        return baseTypeLayouts;
    }

    private static TypeLayoutItemMember? GetLastMemberOffsetFromBaseTypeLayouts(TypeLayoutItem[]? baseTypeLayouts)
    {
        if (baseTypeLayouts is null)
        {
            return null;
        }

        TypeLayoutItemMember? lastFromAnyBaseType = null;

        foreach (var baseType in baseTypeLayouts)
        {
            var lastFromThisBaseType = GetLastMemberByOffsetFromItem(baseType);
            if (lastFromThisBaseType is null)
            {
                continue;
            }

            // If we've not yet found anything to return, or this base type's last member is deeper into the structure than
            // the one we've discovered so far, use the last one from this base type.
            if (lastFromAnyBaseType is null || lastFromThisBaseType.Offset > lastFromAnyBaseType.Offset)
            {
                lastFromAnyBaseType = lastFromThisBaseType;
            }
        }

        return lastFromAnyBaseType;
    }

    private static TypeLayoutItemMember? GetLastMemberByOffsetFromItem(TypeLayoutItem type)
    {
        TypeLayoutItemMember? lastFromAnyBaseType = null;

        if (type.BaseTypeLayouts != null)
        {
            foreach (var baseType in type.BaseTypeLayouts)
            {
                var lastFromThisBaseType = GetLastMemberByOffsetFromItem(baseType);
                if (lastFromThisBaseType is null)
                {
                    continue;
                }

                // If we've not yet found anything to return, or this base type's last member is deeper into the structure than
                // the one we've discovered so far, use the last one from this base type.
                if (lastFromAnyBaseType is null || lastFromThisBaseType.Offset > lastFromAnyBaseType.Offset)
                {
                    lastFromAnyBaseType = lastFromThisBaseType;
                }
            }
        }

        // TODO: This causes a MASSIVE number of allocations (millions of allocations for windows.ui.xaml type loads)
        var lastFromThisType = type.MemberLayouts?.OrderByDescending(x => x.Offset + x.Size).FirstOrDefault();

        if (lastFromAnyBaseType is null)
        {
            // If no base type had any members, we'll use the last one from ourselves (if any)
            return lastFromThisType;
        }
        else if (lastFromThisType is null)
        {
            // If a base type had some members, but this one did not, we'll use the last one from the base types
            return lastFromAnyBaseType;
        }
        // If both the base and this type have members, use the one that's latest (which should always be 'this' type, so we
        // throw in the other case - it would make no sense for a base type to have the last member).
        else if (lastFromAnyBaseType.Offset <= lastFromThisType.Offset)
        {
            return lastFromThisType;
        }
        else if (lastFromThisType.Name == "vfptr")
        {
            // This can happen if we have a vfptr, but the base type doesn't have any virtual functions, and the base type has some data
            // members, since the vfptr always gets shoved as close to the top of the object as possible.  So we can use the last member
            // from our base types because we know the vfptr won't be the last thing (it'll be first).
            return lastFromAnyBaseType;
        }
        else
        {
            // If everything above failed to return something, then some understanding in SizeBench is wrong and could impact code that
            // runs later, so we'll just throw to get a clear signal when this condition hits.
            throw new InvalidOperationException("The last member from a base type was found to be after the last member from 'this' - that seems impossible...");
        }
    }

    private static bool BaseTypeLayoutsContainVfptrAtOffsetAlready(UserDefinedTypeSymbol udt, IEnumerable<TypeLayoutItem>? baseTypeLayouts, uint offset)
    {
        if (udt.VTableCount == 0 || baseTypeLayouts is null)
        {
            return false;
        }

        foreach (var baseType in baseTypeLayouts)
        {
            if (ItemContainsVfptrAtOffsetAlready(baseType, offset))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ItemContainsVfptrAtOffsetAlready(TypeLayoutItem item, uint offset)
    {
        if (item.UserDefinedType.VTableCount == 0)
        {
            return false;
        }

        if (item.MemberLayouts?.Any(m => m.Name == "vfptr" && m.Offset == offset) == true)
        {
            return true;
        }

        if (item.BaseTypeLayouts is null)
        {
            return false;
        }

        foreach (var baseType in item.BaseTypeLayouts)
        {
            if (ItemContainsVfptrAtOffsetAlready(baseType, offset))
            {
                return true;
            }
        }

        return false;
    }
}
