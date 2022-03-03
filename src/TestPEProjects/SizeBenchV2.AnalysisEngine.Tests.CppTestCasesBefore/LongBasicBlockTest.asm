.list
altentry SomeAltEntry

; This tests the case to have an extremely long basic block (>100 instructions in one block) to test DbgX disassembly
; when we have to go fetch the "Next"/"Expand" part of the disassembly within a block.
_TEXT$00 segment para 'CODE'
align   16
public  asmVeryLongBasicBlock
asmVeryLongBasicBlock    proc    frame

       prefetchw [rcx]

       push    rbx
       .pushreg rbx

.endprolog

  mov eax, 7
  add eax, 8

  lock inc DWORD PTR [eax + 4]

  ; just spamming instructions now to get over 100
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

Middle:  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8
  add eax, 8

SomeAltEntry:

   lock cmpxchg16b [r10]
        jnz     short Middle
LastMainInstr:  mov     rax, r8

        .beginepilog


        pop     rbx
        ret

WithinEpilogue:
   lock or      qword ptr [r10], 0
        jmp     short LastMainInstr

asmVeryLongBasicBlock ENDP

_TEXT$00 ends

    


; Having a second procedure with 'align 16' is important to allow us to test the weird case where the public symbol for the label above
; (SomeAltEntry), has an RVA+length that goes past the end of asmVeryLongBasicBlock, which some Windows OS binaries exhibit, so this is
; needed for a minimal repro.  Don't remove this, even though it's unreferenced in tests directly!
_TEXT$00 segment para 'CODE'
        db      6 dup (0cch)
        align   16
        public  AnotherAsmProc

AnotherAsmProc    proc    frame
    prefetchw [rcx]

    push    rbx
    .pushreg rbx

    .endprolog

  mov     r10, rcx

.beginepilog
        pop     rbx
        ret

AnotherAsmProc    endp

_TEXT$00 ends

END