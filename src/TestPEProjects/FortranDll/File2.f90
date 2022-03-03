!  File2.f90 
!
!  FUNCTIONS/SUBROUTINES exported from FortranDll1.dll:
!  Subroutine2 - subroutine 
!
!  A test case that passes a bunch of FORTRAN's primitive types around
subroutine SUBROUTINE2(SOME_DOUBLE, SOME_COMPLEX, SOME_DOUBLE_COMPLEX, SOME_LOGICAL, SOME_LOGICAL_KIND_4)
	implicit none
	double precision, intent(in) :: SOME_DOUBLE
	complex, intent(in) :: SOME_COMPLEX
    double complex, intent(in) :: SOME_DOUBLE_COMPLEX
    logical, intent(in) :: SOME_LOGICAL
	logical(kind=4), intent(in) :: SOME_LOGICAL_KIND_4

  ! Expose subroutine SUBROUTINE2 to users of this DLL
  !
  !DEC$ ATTRIBUTES DLLEXPORT::SUBROUTINE2

 ! Body of SUBROUTINE2
	print *, 'Hello World!'

end subroutine SUBROUTINE2
    
! A test case for strings since they pass hidden parameters with horrid names, and deferred-length strings (len=:) end up generating a
! SymTagCustomType in DIA which is difficult to test any other way.
subroutine SUBROUTINE3(SOME_STRING, SOME_DEFERRED_LEN_STR)
    implicit none
	character(len=10), intent(in) :: SOME_STRING
    character(len=:), pointer :: SOME_DEFERRED_LEN_STR
  
    ! Expose subroutine SUBROUTINE3 to users of this DLL
    !
    !DEC$ ATTRIBUTES DLLEXPORT::SUBROUTINE3
    
    ! Body of SUBROUTINE3
	print *, 'Hello World!'

end subroutine SUBROUTINE3