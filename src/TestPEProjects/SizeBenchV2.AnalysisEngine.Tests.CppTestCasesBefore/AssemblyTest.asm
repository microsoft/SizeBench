_text SEGMENT

extrn ExitProcess: proc

.code
asmProc PROC FRAME
  .endprolog

  mov eax, 7
  add eax, 8

  ; This tests the case where MASM generates a SymTagPointer, which points to a SymTagNull, by having
  ; a data pointer in the middle of code.
public MyTestEntry
MyTestEntry label ptr proc

  xor ecx,ecx
  call ExitProcess
asmProc ENDP

_text ENDS

END