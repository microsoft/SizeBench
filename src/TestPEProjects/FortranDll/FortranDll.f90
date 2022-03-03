!  FortranDll.f90 
!
!  FUNCTIONS/SUBROUTINES exported from FortranDll.dll:
!  SUBROUTINE1 - subroutine 
!
!  A test case that passes around arrays and matrices that are higher-rank, including when the rank is another one of the parameters to test named upper/lower bounds in DIA
subroutine SUBROUTINE1(A_SHORT, AN_INT, ARR_OF_INT64, SIMPLE_MATRIX, MATRIX_WITH_FANCY_BOUNDS, MATRIX_WITH_PARAM_BOUNDS)
	implicit none
	integer(kind=2), intent(in) :: A_SHORT
	integer(kind=4), intent(in) :: AN_INT
	integer(kind=8), intent(in), dimension(6) :: ARR_OF_INT64
	real, intent(in) :: SIMPLE_MATRIX(4,4)
	integer, intent(in), dimension(-3:2,0:4) :: MATRIX_WITH_FANCY_BOUNDS
	real, intent(in), dimension(A_SHORT,AN_INT) :: MATRIX_WITH_PARAM_BOUNDS

  ! Expose subroutine SUBROUTINE1 to users of this DLL
  !
  !DEC$ ATTRIBUTES DLLEXPORT::SUBROUTINE1

  ! Variables

 ! Body of SUBROUTINE1
	print *, 'Hello World!'

end subroutine SUBROUTINE1
