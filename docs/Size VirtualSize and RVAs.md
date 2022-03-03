# Size, VirtualSize, and Relative Virtual Addresses (RVAs)
There are three core words in the vocabulary within the engine - RVA, Size and
VirtualSize.  These all come from the PE format as documented 
[here](https://docs.microsoft.com/windows/win32/debug/pe-format).

Binaries have two kinds of size that users care about - how much space they take
up on disk and how much space they take once loaded into memory in a running
program.  Size-on-disk is called "Size" in PE files and thus in SizeBench's Analysis
Engine and Object Model.  Size-in-memory is called "VirtualSize" and is generally
equal to or larger than Size-on-disk.

The Size of a binary is determined by the sum of each BinarySection's Size.
Each section is padded up be the FileAlignment, which defaults to 512 bytes
(a floppy disk sector), though it can be modified with 
[/FILEALIGN](https://docs.microsoft.com/cpp/build/reference/filealign?view=vs-2019).
It is not recommended to change /FILEALIGN but SizeBench does its best to handle
this.  So, to put this all together, what it means is that if you have a .text
section with 100 bytes of code in it, it will take up 512 bytes on disk with 412
of them being padding.

The VirtualSize of a binary is determined by the sum of each BinarySection's
VirtualSize.  Each section, when loaded into memory, must occupy different pages in
the memory manager, as that is the only granularity with which the OS can enforce
read-only vs. read-write memory, executable vs. not-executable, and so on.  Pages
are 4K-aligned by default, though you can ask for more with
[/ALIGN](https://docs.microsoft.com/cpp/build/reference/align-section-alignment?view=vs-2019)
or 
[/SECTION](https://docs.microsoft.com/cpp/build/reference/section-specify-section-attributes?view=vs-2019).
As above, it's not recommended to change /ALIGN or /SECTION alignment, but SizeBench
does its best to handle these.

VirtualSize has one other interesting property - it need not exist on-disk at all!
A good example is the ".bss" COFF Group (block started by symbol).  This is
read-write data that is zero-initialized.  So when these pages are mapped into a
running process, they get zero-initialized by the loader, but they don't need to
exist on-disk as a bunch of zeros.  The way this is accomplished is by putting 
.bss and other VirtualSize-only COFF Groups at the end of the .data section.  They
will start using up bytes that are padding up to the SectionAlignment, or cause
additional pages to be created and zero-initialized if there's enough to need that.

Putting this all together for VirtualSize, it means that you can have a .data section
that looks like this:

| Name      | Size   | VirtualSize   | VirtualSizeIncludingPadding |
|-----------|-------:|--------------:|----------------------------:|
| .data     |    150 |          6150 |                        8192 |
|  .data$r  |    100 |           100 |                         100 |
|  .data$zz |     50 |            50 |                          50 |
|  .bss     |      0 |          6000 |                        8042 |

So .data\$r will come first, using up 100 bytes of space on-disk and in-memory.  Then
.data\$zz will come next, using up another 50 bytes on-disk and in-memory.  That means
we'll have 362 bytes of padding on-disk (512-150), to get to the end of the section,
assuming default FileAlignment of 512 bytes.  The .bss COFF Group is 6000 bytes long,
but exists only in-memory, it takes up no space on-disk, as it's all zeros initially.
The Section in-memory will be padded out to 4096 bytes to be 4K-aligned (to fit on
physical pages).  So that leaves 3,946 bytes of padding after .data\$zz (4096-150).
So of that 6000, 3,946 bytes would fit into this existing "dead space" - but that's
not enough, so another physical page will get created, bumping the total VirtualSize
of .data to 6,150 bytes and another 2,042 bytes of padding.

In the SizeBench AnalysisEngine, this amount of complexity is why we're so careful to
track Size, VirtualSize, and VirtualSizeIncludingPadding separately.  They all matter
to somebody - Size matters to those that want to optimize on-disk size, or the amount
of binary transmitted over the wire for updates, stuff like that.  VirtualSize matters
to somebody who wants to optimize memory usage at runtime.  Padding explains where some
"dead spots" are, to understand if growth/shrinkage will "spill over" a page boundary.

So now that we understand Size and VirtualSize, let's talk about Relative Virtual
Addresses, or RVAs.  An RVA is the way to address a location in a binary, but it assumes
that the padding done by the loader to VirtualSizeIncludingPadding from above has already
occurred.  So if you try to reference into a binary by just loading it as a stream off
disk and saying "(binary stream start) + RVA" then you are not going to get the right
answer.  For this reason, SizeBench uses the OS loader to bring the binary into memory
(without executing anything) so we can address into specific bytes correctly by simply
adding the RVA.

This means that when looking at a range specified by (RVAStart, RVAEnd) it may not all
exist on-disk, some of it may only exist when loaded into memory.  Luckily, the way the
linker tracks these and creates Contribtions in a binary, it keeps virtual ranges and
physical ranges separate, so every RVARange in SizeBench exists either fully in Size-space
or fully in VirtualSize-space.