using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SizeBench.AnalysisEngine;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct RVARange : IEquatable<RVARange>
{
    private string DebuggerDisplay => $"RVA Range: Start=0x{this.RVAStart:X}, End=0x{this.RVAEnd:X}, Size={this.Size:N0}, VirtualSize={this.VirtualSize:N0}";

    // It's important that these fields be readonly - they need to be immutable as RVARange objects are treated as if they're immutable.
    // This class overrides GetHashCode() which must only operate on immutable state for dictionaries to not get corrupted buckets - so don't
    // ever make these mutable.
#pragma warning disable IDE0032 // Use auto property - these need to be fields both because of the note above about readonly and also because they're
    // very perf-sensitive so the overhead of get/set on a property is just too high for these 3.
    private readonly uint _rvaStart;
    private readonly uint _rvaEnd;
    private readonly bool _isVirtualSize;
#pragma warning restore IDE0032 // Use auto property

    public readonly uint Size => this.IsVirtualSize ? 0 : this.VirtualSize;
    public readonly uint VirtualSize => this._rvaEnd - this._rvaStart + 1;

#pragma warning disable IDE0032 // Use auto property - these need to be fields both because of the note above about readonly and also because they're
    // very perf-sensitive so the overhead of get/set on a property is just too high for these 3.
    public readonly uint RVAStart => this._rvaStart;
    public readonly uint RVAEnd => this._rvaEnd;

    // If this is true, then this RVARange exists only in "virtual size" space - it does not take up any space on disk, but it does take
    // up space in memory.  An example would be the RVARanges composing the .bss COFF Group.
    public readonly bool IsVirtualSize => this._isVirtualSize;
#pragma warning restore IDE0032 // Use auto property

    /// <summary>
    /// Creates an instance of RVARange with Start and End RVA's are included in the range.
    /// For example:
    /// Range [0, 50] starts at 0 and ends at 50 and has a size of (50 - 0 + 1) = 51.
    /// And Range [100, 149] starts at 100 and ends at 149 and has a size of (149 - 100 + 1) = 50.  
    /// </summary>
    /// <param name="rvaStart">RVA where the range starts.</param>
    /// <param name="rvaEnd">RVA where the range ends and is inclusive.</param>
    /// <param name="isVirtualSize">True if this exists only in memory, not on-disk (such as .bss)</param>
    public RVARange(uint rvaStart, uint rvaEnd, bool isVirtualSize = false)
    {
        this._rvaStart = rvaStart;
        this._rvaEnd = rvaEnd;
        this._isVirtualSize = isVirtualSize;
    }

    public static RVARange FromRVAAndSize(uint rvaStart, uint size, bool isVirtualSize = false)
        => new RVARange(rvaStart, rvaStart + (size == 0 ? size : size - 1), isVirtualSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(uint rva)
    {
        return rva >= this._rvaStart &&
               rva <= this._rvaEnd;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(uint rva, uint size)
    {
        return rva >= this._rvaStart &&
               rva + size - 1 <= this._rvaEnd;
    }

#pragma warning disable CA1062 // Validate arguments of public methods - this function is just too hot for perf to afford to check for null, we'll let it crash if anyone passes in null.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(RVARange rvaRange)
    {
        return rvaRange!._rvaStart >= this._rvaStart &&
               rvaRange!._rvaEnd <= this._rvaEnd;
    }
#pragma warning restore CA1062


    internal bool IsAdjacentTo(RVARange rvaRange, uint maxPaddingAllowed = 1)
    {
        // If they're exactly equal (rvaRange.RVAStart == lastSeen.RVAEnd) then they clearly are adjacent,
        // but of course they should also be considered adjacent if they're off by some padding bytes as that is the
        // same.  For example having (0, 10) and (11, 20) should merge to (0, 20).  So we check
        // that rvaRange.RVAStart - lastSeen.RVAEnd <= maxPaddingAllowed to account for both the true 'overlapping'
        // case as well as the 'adjacent' case.
        if (this._rvaStart < rvaRange!._rvaStart)
        {
            return rvaRange!._rvaStart - this._rvaEnd <= maxPaddingAllowed;
        }
        else if (rvaRange!._rvaStart < this._rvaStart)
        {
            return this._rvaStart - rvaRange!._rvaEnd <= maxPaddingAllowed;
        }
        else
        {
            return false; // they have the same start, so they can't be adjacent by definition
        }
    }

    internal bool CanBeCombinedWith(RVARange rvaRange, uint maxPaddingAllowed = 1)
    {
        if (this._isVirtualSize != rvaRange._isVirtualSize)
        {
            return false;
        }

        if (IsAdjacentTo(rvaRange, maxPaddingAllowed))
        {
            return true;
        }

        // They're not adjacent, but they may still overlap
        return Contains(rvaRange.RVAStart) || Contains(rvaRange.RVAEnd) || rvaRange.Contains(this.RVAStart) || rvaRange.Contains(this.RVAEnd);
    }

    internal RVARange CombineWith(RVARange rvaRange, uint maxPaddingAllowed = 1)
    {
        Debug.Assert(IsAdjacentTo(rvaRange, maxPaddingAllowed) || Contains(rvaRange.RVAStart) || Contains(rvaRange.RVAEnd));

        if (this._isVirtualSize != rvaRange!._isVirtualSize)
        {
            throw new InvalidOperationException("Cannot merge virtual and non-virtual RVA Ranges, that doesn't make sense!");
        }

        return new RVARange(Math.Min(this._rvaStart, rvaRange._rvaStart),
                            Math.Max(this._rvaEnd, rvaRange._rvaEnd),
                            this._isVirtualSize);
    }

    internal RVARange ExpandEndTo(uint newRVAEnd)
        => new RVARange(this._rvaStart, Math.Max(this._rvaEnd, newRVAEnd), this._isVirtualSize);

    #region Implementing Value Equality

    // See https://docs.microsoft.com/dotnet/csharp/programming-guide/statements-expressions-operators/how-to-define-value-equality-for-a-type

    public override bool Equals(object? obj)
    {
        if (obj is RVARange otherRange)
        {
            return Equals(otherRange);
        }

        return false;
    }

    public bool Equals(RVARange other)
    {
        return this._rvaStart == other._rvaStart &&
               this._rvaEnd == other._rvaEnd &&
               this._isVirtualSize == other._isVirtualSize;
    }

    public static bool operator ==(RVARange? lhs, RVARange? rhs)
    {
        // Check for null on left side.
        if (lhs is null)
        {
            return rhs is null;
        }

        // Equals handles case of null on right side.
        return lhs.Equals(rhs);

    }

    public static bool operator !=(RVARange? lhs, RVARange? rhs) => !(lhs == rhs);

    public override int GetHashCode()
        => (int)(this._rvaStart ^ this._rvaEnd ^ this._isVirtualSize.GetHashCode());

    #endregion

    public override string ToString() => $"0x{this._rvaStart:X} - 0x{this._rvaEnd:X}";
}
