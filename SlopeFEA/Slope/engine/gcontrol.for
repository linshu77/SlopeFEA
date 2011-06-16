!***********************************************************************
! PROJECT:     SlopeFEA (c) 2011 Brandon Karchewski
!              Licensed under the Academic Free License version 3.0
!                  http://www.opensource.org/licenses/afl-3.0.php
! 
! CONTACT:     Brandon Karchewski
!              Department of Civil Engineering
!              McMaster University, JHE-301
!              1280 Main St W
!              Hamilton, Ontario, Canada
!              L8S 4L7
!              p: 905-525-9140 x24287
!              f: 905-529-9688
!              e: karcheba@mcmaster.ca
!              
! 
! SOURCE INFORMATION:
! 
! The repository for this software project is hosted on git at:
!      
!      git://github.com/karcheba/SlopeFEA
!      
! As such, the code for the project is free and open source.
! The relevant license is AFLv3 (see link above). See the
! README file in the root directory of the repository for a
! detailed project description, acknowledgements, references,
! and the revision history.
!***********************************************************************
!
!
      MODULE gcontrol
      USE numeric     ! number types and constants
      USE mproperty   ! material property data
      USE nodes       ! node data
      USE elements    ! element data
      USE tractions   ! applied load data
!
!$    USE OMP_LIB         ! parallel lib (gfortran: compile with -fopenmp switch)
!
      IMPLICIT NONE
!
      INTEGER, PARAMETER :: output=2,mtl=3,nod=4,ele=5,bel=6,his=7  ! file unit numbers
      CHARACTER(LEN=64) :: ANTYPE   ! string denoting analysis type
!
      INTEGER(ik), SAVE :: NSTEP    ! # of load steps
      INTEGER(ik), SAVE :: NITER    ! # of iterations
      INTEGER(ik), SAVE :: NPRINT   ! # of print lines
!
      REAL(dk), SAVE :: LFACT       ! load factor
      REAL(dk), SAVE :: GFACT       ! gravity factor
!
      INTEGER(ik), SAVE :: NNET     ! # of system dofs (computed in INPUT)
      INTEGER(ik), SAVE :: LBAND    ! # of co-diagonal bands in stiff mat (computed in BANDWH)
!
      REAL(dk), ALLOCATABLE :: TLOAD(:), GLOAD(:)   ! load vecs
      REAL(dk), ALLOCATABLE :: GLOAD0(:)            ! for non-linear stepping
      REAL(dk), ALLOCATABLE :: STR(:)               ! internal forces
      REAL(dk), ALLOCATABLE :: DISP(:), TDISP(:)        ! disp (and/or vel,acc,press,temp,etc.) vecs
      REAL(dk), ALLOCATABLE :: GSTIF(:,:), fGSTIF(:,:)  ! global stiffness mat
      REAL(dk), ALLOCATABLE :: ESTIF(:,:)               ! element stiffness mat
      INTEGER(ik), SAVE :: HBW    ! half bandwidth of stiff mat (LBAND+1)
!	
!
!
      CONTAINS
!
!
! ......................................................................
! .... INPUT ...........................................................
! ......................................................................
!     puts information from data input files into appropriate storage
! ......................................................................
      SUBROUTINE INPUT (fpath)
!
      IMPLICIT NONE
!
      CHARACTER*(*), INTENT(IN) :: fpath        ! input file path
      INTEGER(ik) :: i,i1,i2,j      ! loop variables
      INTEGER(ik) :: ntot           ! total possible dofs
      INTEGER(ik) :: currtime(8)    ! for printing analysis date/time
      REAL(dk), ALLOCATABLE :: lcoords(:,:)   ! for computing element area
      INTEGER(ik) :: ierr   ! for check allocation status
!
!     open input file units
      OPEN(mtl,     FILE=fpath(1:LEN(fpath)-1)//".mtl")
      OPEN(nod,     FILE=fpath(1:LEN(fpath)-1)//".nod")
      OPEN(ele,     FILE=fpath(1:LEN(fpath)-1)//".ele")
      OPEN(bel,     FILE=fpath(1:LEN(fpath)-1)//".bel")
!
!     open and clean output file units
      OPEN(output,  FILE=fpath(1:LEN(fpath)-1)//".out");  REWIND(output)
      OPEN(his,     FILE=fpath(1:LEN(fpath)-1)//".his");  REWIND(his);
!
!     *********************************
!     ********* CONTROL DATA **********
!     *********************************
      READ(mtl,*) NMTL          ! # of materials
      ALLOCATE( GRR(NMTL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating GRR."
      ALLOCATE( PHI(NMTL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating PHI."
      ALLOCATE( COH(NMTL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating COH."
      ALLOCATE( PSI(NMTL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating PSI."
      ALLOCATE( EMOD(NMTL), STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating EMOD."
      ALLOCATE( NU(NMTL),   STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating NU."
      GRR(:)    = 0.0D0
      PHI(:)    = 0.0D0
      COH(:)    = 0.0D0
      PSI(:)    = 0.0D0
      EMOD(:)   = 0.0D0
      NU(:)     = 0.0D0
!
      READ(nod,*) NNOD, NDIM, NVAR, IPRINT,   ! # nodes, # dimensions, # dofs/node, print node
     +            NSTEP, NITER, NPRINT,       ! # load steps, # iterations, # print lines
     +            LFACT, GFACT                ! load factor, gravity factor
      ALLOCATE( COORDS(NDIM,NNOD),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating COORDS."
      ALLOCATE( PLOADS(NDIM,NNOD),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating PLOADS."
      ALLOCATE( IX(NNOD*NVAR),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating IX."
      ALLOCATE( EVOL(NNOD),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating EVOL."
      ALLOCATE( EVOL0(NNOD),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating EVOL0."
      ALLOCATE( EVOLi(NNOD),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating EVOLi."
      COORDS(:,:)   = 0.0D0           ! alloc and init grid data storage
      PLOADS(:,:)   = 0.0D0
      IX(:)         = 0
      EVOL(:)       = 0.0D0
      EVOL0(:)      = 0.0D0
      EVOLi(:)      = 0.0D0
      IF (GFACT .LT. 0.0) GFACT = 0.0D0
      IF (LFACT .LT. 0.0) LFACT = 0.0D0     ! ensure solution parameters are meaningful
      IF (LFACT .LT. 1.0D-8 .OR. NELT .LT. 1) GFACT = 1.0D0
      IF (NSTEP .LT. 2) NSTEP = 2
      IF (NITER .LT. 2) NITER = 2
!
      READ(ele,*) NEL, NNODEL   ! # body elements, # nodes per element
      NVEL = NVAR*NNODEL    ! compute #dofs/element
      NNN = NNODEL+1        ! compute #nodes+mtl type/element
      ALLOCATE( LJ(NVEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating LJ."
      ALLOCATE( ICO(NNN,NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating ICO."
      ALLOCATE( AREA(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating AREA."
      ALLOCATE( CENT(NDIM,NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating CENT."
      ALLOCATE( EVOLB(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating EVOLB."
      ALLOCATE( SXX(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating SXX."
      ALLOCATE( SYY(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating SYY."
      ALLOCATE( SXY(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating SXY."
      ALLOCATE( SZZ(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating SZZ."
      ALLOCATE( FBAR(NEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating FBAR."
      ALLOCATE( lcoords(NDIM,NNODEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating lcoords."
      LJ(:)         = 0                     ! alloc and init element data storage
      ICO(:,:)      = 0
      AREA(:)       = 0.0D0
      CENT(:,:)     = 0.0D0
      EVOLB(:)      = 0.0D0
      SXX(:)        = 0.0D0
      SYY(:)        = 0.0D0
      SXY(:)        = 0.0D0
      SZZ(:)        = 0.0D0
      FBAR(:)       = 0.0D0
      lcoords(:,:)  = 0.0D0
!
      READ(bel,*) NELT, NNODELT     ! # traction elements, # nodes/traction element
      NVELT = NVAR*NNODELT    ! compute # dofs per traction element
      ALLOCATE( ICOT(NNODELT,NELT),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating ICOT."
      ALLOCATE( TNF(NNODELT,NELT),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating TNF."
      ALLOCATE( TSF(NNODELT,NELT),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating TSF."
      ICOT(:,:) = 0                     ! alloc and init traction data storage
      TNF(:,:)  = 0.0D0
      TSF(:,:)  = 0.0D0
!
!     write output file headers
      CALL DATE_AND_TIME(VALUES=currtime)
      WRITE(output,100)     NNODEL,
     +                      fpath(1:LEN(fpath)-1),
     +                      currtime(2), currtime(3), currtime(1),
     +                      currtime(5:8)
      WRITE(his,100)        NNODEL,
     +                      fpath(1:LEN(fpath)-1),
     +                      currtime(2), currtime(3), currtime(1),
     +                      currtime(5:8)
!
!     write control data to output file
      WRITE(output,101) ANTYPE, NMTL, NNOD, NDIM, NVAR,
     +                  NEL, NNODEL, NVEL, NELT, NNODELT, NVELT,
     +                  IPRINT, NSTEP, NITER, NPRINT,
     +                  LFACT, GFACT
!
!     *********************************
!     *********** NODE DATA ***********
!     *********************************
      WRITE(output,110);  WRITE(output,111) ! node data headers
      DO i = 1,NNOD
        i2 = NVAR*i     ! indices for fixity/node numbering vector
        i1 = i2-1
        READ(nod,*) j,
     +              COORDS(:,i),    ! node locations
     +              IX(i1:i2),      ! fixity data
     +              PLOADS(:,i)     ! point load data
      END DO
      ntot = NVAR*NNOD;     ! compute total possible dofs
      NNET = 0              ! initialize actual total dofs
      DO i = 1,ntot     ! loop through possible dofs
        IF (IX(i) .GT. 0) THEN  ! if the dof is not fixed
            NNET = NNET+1   ! increment actual total dofs
            IX(i) = NNET    ! label the node
        END IF
      END DO
      DO i = 1,NNOD
        i2 = NVAR*i     ! indices for fixity/node numbering vector
        i1 = i2-1
        WRITE(output,112) i, COORDS(:,i), IX(i1:i2), PLOADS(:,i)
      END DO
!
!     *********************************
!     ********* ELEMENT DATA **********
!     *********************************
      WRITE(output,120);  WRITE(output,121) ! element data headers
      DO i = 1,NEL
        READ(ele,*) j, ICO(:,i)     ! read connect/mtl data for each element
        DO j = 1,NNODEL
            lcoords(:,j) = COORDS(:,ICO(j,i))   ! get vertex coords
        END DO
        AREA(i) = lcoords(1,NNODEL)*lcoords(2,1) 
     +                  - lcoords(1,1)*lcoords(2,NNODEL)    ! compute element area
        DO j = 2,NNODEL
            AREA(i) = AREA(i)   + lcoords(1,j-1)*lcoords(2,  j)
     +                          - lcoords(1,  j)*lcoords(2,j-1)
        END DO
        AREA(i) = 0.5D0*AREA(i)
        DO j = 1,NDIM
          CENT(j,i) = SUM(lcoords(j,:))/NNODEL    ! compute element centroid
        END DO
        WRITE(output,122) i, ICO(:,i), AREA(i), CENT(:,i)
      END DO
      DEALLOCATE(lcoords, STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in deallocating lcoords."
!
      CALL BANDWH()     ! compute number of codiagonal bands
      WRITE(output,130) 
      WRITE(output,131) NNET, LBAND ! write stiffness matrix stats to output
      ALLOCATE( TLOAD(NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating TLOAD."
      ALLOCATE( GLOAD(NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating GLOAD."
      ALLOCATE( STR(NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating STR."
      ALLOCATE( GLOAD0(NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating GLOAD0."
      ALLOCATE( DISP(NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating DISP."
      ALLOCATE( TDISP(NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating TDISP."
      ALLOCATE( GSTIF(HBW,NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating GSTIF."
      ALLOCATE( fGSTIF(HBW,NNET),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating fGSTIF."
      ALLOCATE( ESTIF(NVEL,NVEL),  STAT=ierr)
      IF (ierr .NE. 0)  WRITE(output,*) "Error in allocating ESTIF."
      TLOAD(:)    = 0.0D0
      GLOAD(:)    = 0.0D0
      STR(:)      = 0.0D0
      GLOAD0(:)   = 0.0D0               ! alloc and init solution space
      DISP(:)     = 0.0D0
      TDISP(:)    = 0.0D0
      GSTIF(:,:)  = 0.0D0
      fGSTIF(:,:) = 0.0D0
      ESTIF(:,:)  = 0.0D0
!
!     *********************************
!     ********* MATERIAL DATA *********
!     *********************************
      WRITE(output,140)   ! material data header
      DO i = 1,NMTL
        READ(mtl,*) GRR(i), PHI(i), COH(i), PSI(i), EMOD(i), NU(i)    ! get data from file
        WRITE(output,141) i, GRR(i), PHI(i), COH(i),
     +                    PSI(i) EMOD(i), NU(i)
        
        IF (COH(i) .LT. 1.0D-8) COH(i) = 1.0D-8   ! ensure small cohesion for numerical stability
        COH(i) = COH(i)*COS(degTOrad*PHI(i))
        PHI(i) = SIN(degTOrad*PHI(i))             ! pre-calculate trig functions of material props
        PSI(i) = SIN(degTOrad*PSI(i))
        
      END DO
!
!     *********************************
!     ********* TRACTION DATA *********
!     *********************************
      WRITE(output,150);  WRITE(output,151) ! traction element headers
      DO i = 1,NELT
        READ(bel,*) j,
     +              ICOT(:,i),  ! read traction element data
     +              TNF(:,i),
     +              TSF(:,i)
        WRITE(output,152) i, ICOT(:,i), TNF(:,i), TSF(:,i)
      END DO
!
!     *********************************
!     ****** OUTPUT FILE FORMATS ******
!     *********************************
!     header
  100 FORMAT(/, '====================================================',
     +       /, 6X, I1, '-NODED FINITE ELEMENT STRESS ANALYSIS',
     +       /, '====================================================',
     +       //, 'FILE: ', A,
     +       /, 'DATE: ', I2, '/', I2, '/', I4,
     +       /, 'TIME: ', I2, ':', I2, ':', I2, '.', I3, // )
!
!     control information
  101 FORMAT(/, 1X, 'type of analysis ...................... = ', A,
     +       /, 1X, '# of material types .............. NMTL = ', I7,
     +       /, 1X, '# of nodes ....................... NNOD = ', I7,
     +       /, 1X, '# of coordinate dimensions ....... NDIM = ', I7,
     +       /, 1X, '# of dofs/node ................... NVAR = ', I7,
     +       /, 1X, '# of body elements ................ NEL = ', I7,
     +       /, 1X, '# of nodes/element ............. NNODEL = ', I7,
     +       /, 1X, '# of dofs/element ................ NVEL = ', I7,
     +       /, 1X, '# of traction elements ........... NELT = ', I7,
     +       /, 1X, '# of nodes/traction element ... NNODELT = ', I7,
     +       /, 1X, '# of dofs/traction element ...... NVELT = ', I7,
     +       /, 1X, '# of load steps ................. NSTEP = ', I7,
     +       /, 1X, '# of iterations/load step ....... NITER = ', I7,
     +       /, 1X, '# of print lines ............... NPRINT = ', I7,
     +       /, 1X, 'output node number ............. IPRINT = ', I7,
     +       /, 1X, 'load factor ..................... LFACT = ', E12.5,
     +       /, 1X, 'gravity factor .................. GFACT = ', E12.5,
     +       / )
!
!     node data
  110 FORMAT(//,
     +  '===================== NODE INFORMATION =====================')
  111 FORMAT(//, 3X, 'NODE', 8X, 'COORDS (X,Y,..)', 8X, 'FIX (U,V,..)',
     +        8X, 'PLOADS (U,V,..)', //)
  112 FORMAT(1X, I5, 5X, F10.3, 2X, F10.3, 5X, 2I5,
     +        5X, F10.3, 2X, F10.3, /)
!
!     element data
  120 FORMAT(//,
     +  '================= BODY ELEMENT INFORMATION =================')
  121 FORMAT(//, 3X, 'ELEMENT', 9X, 'NODES', 9X, 'MTL', 8X, 'AREA',
     +        8X, 'CENTROID (X,Y,..)', //)
  122 FORMAT(1X, I5, 5X, 3I5, 5X, I3, 5X, E12.4, 5X, 2F10.3, /)
!
!     stiffness matrix statistics (packed band storage)
  130 FORMAT(//,
     +  '=============== STIFFNESS MATRIX INFORMATION ===============')
  131 FORMAT(//, 1X, '# of degrees of freedom ... NNET = ', I7,
     +        /, 1X, '# of codiagonal bands .... LBAND = ', I7, /)
!
  140 FORMAT(//,
     +  '==================== MATERIAL PROPERTIES ===================')
  141 FORMAT(//, 'MATERIAL SET', I5,
     +        /, '*************************',
     +        /, 'unit weight ............... GRR = ', E12.5,
     +        /, 'internal friction angle ... PHI = ', E12.5,
     +        /, 'cohesion .................. COH = ', E12.5,
     +        /, 'dilatancy angle ........... PSI = ', E12.5,
     +        /, 'elastic modulus .......... EMOD = ', E12.5,
     +        /, 'poisson''s ratio ............ NU = ', E12.5  )
!
  150 FORMAT(///,
     +  '=============== TRACTION ELEMENT INFORMATION ===============')
  151 FORMAT(//, 3X, 'ELEMENT', 6X, 'NODES', 15X, 'TNF', 20X, 'TSF', //)
  152 FORMAT(1X, I5, 5X, 2I5, 5X, 2E12.4, 5X, 2E12.4, /)
!
      RETURN
!
      END SUBROUTINE INPUT
!
!
! ......................................................................
! .... CLEANUP .........................................................
! ......................................................................
!     deallocate memory, rewind data units, close data files
! ......................................................................
      SUBROUTINE CLEANUP ()
!
      DEALLOCATE( GRR, PHI, COH, PSI, EMOD, NU )  ! material data
      REWIND(mtl);    CLOSE(mtl)
!
      DEALLOCATE( COORDS, PLOADS, IX )            ! node data
      REWIND(nod);    CLOSE(nod)
!
      DEALLOCATE( LJ, ICO, AREA, CENT,
     +            SXX, SYY, SXY, SZZ )            ! body element data
      REWIND(ele);    CLOSE(ele)
!
      DEALLOCATE( ICOT, TNF, TSF )                ! traction element data
      REWIND(bel);    CLOSE(bel)
!
      REWIND(output); CLOSE(output)               ! output file
!
      REWIND(his);    CLOSE(his)                  ! load step history file
!
!     solution space
      DEALLOCATE( TLOAD, GLOAD, STR, GLOAD0, DISP, TDISP, 
     +            GSTIF, fGSTIF, ESTIF )
!
      RETURN
!
      END SUBROUTINE CLEANUP
!
!
! ......................................................................
! .... BANDWH ..........................................................
! ......................................................................
!     computes the number of co-diagonal bands of the stiffness matrix
!     (needed for storing the matrix in packed format to
!     make use of efficient solvers)
! ......................................................................
      SUBROUTINE BANDWH ()
!
      IMPLICIT NONE
!
      INTEGER :: lmin, lmax     ! connectivity stats
      INTEGER :: i,j,k          ! loop variables      
      INTEGER :: k1, lbcurr     ! begin index, current band
!
!     initialize bandwidth
      LBAND = 0
!
!$OMP PARALLEL PRIVATE(lmin,lmax,j,k,k1,lbcurr)
!$OMP DO
      DO i = 1,NEL    ! check the bandwidth for each element
!
!       obtain element connectivity
        DO j = 1,NVAR   ! dofs per node
          DO k = 1,NNODEL ! nodes per element
!
            k1 = (k-1)*NVAR ! initial index
            LJ(j+k1) = IX(NVAR*ICO(k,i)-NVAR+j) ! get global node number
!
          END DO
        END DO
!
!       initialize index statistics
        lmax = 0
        lmin = HUGE(1)   ! largest integer
!
        DO j = 1,NVEL   ! dof per element
!	    
!           skip if the dof is restrained (i.e. not present in stiffness matrix)
	        IF (LJ(j) .EQ. 0) CYCLE
!
!           compare and update as appropriate
	        IF (LJ(j)-lmax .GT. 0) lmax = LJ(j)  
	        IF (LJ(j)-lmin .LT. 0) lmin = LJ(j)  
!
        END DO
!
        lbcurr = lmax-lmin
!$OMP ATOMIC    ! only one thread may update at a time
        IF (lbcurr .GT. LBAND) LBAND = lbcurr
!
      END DO
!$OMP END DO
!$OMP END PARALLEL
!
      HBW = LBAND+1
!
      RETURN
!
      END SUBROUTINE BANDWH
!
!
! ......................................................................
! .... LOCAL ...........................................................
! ......................................................................
!     gets local coords and material type of given element, iel
! ......................................................................
      SUBROUTINE LOCAL (iel, lcoords, mtype)
!
      IMPLICIT NONE
!
      INTEGER(ik), INTENT(IN) :: iel          ! element number
      INTEGER(ik), INTENT(OUT) :: mtype       ! material type
      REAL(dk), INTENT(OUT) :: lcoords(:,:)   ! local coords
      INTEGER(ik) :: i, i1, i2, j    ! loop variables
!
      lcoords(1:NDIM,1:NNODEL) = COORDS(1:NDIM,ICO(1:NNODEL,IEL)) ! get local coords
      mtype = ICO(NNN,IEL)    ! get material type
!
!$OMP PARALLEL PRIVATE(i,i1,i2)
!$OMP DO
!     get local connectivity
      DO j = 1,NNODEL   ! element nodes
        i1 = NVAR*(j-1)   ! local offset
        i2 = NVAR*(ICO(j,iel)-1)  ! global offset
        DO i = 1,NVAR   ! dofs
          LJ(i+i1) = IX(i+i2)   ! map global to local
        END DO  ! dofs
      END DO  ! element nodes
!$OMP END DO
!$OMP END PARALLEL
!
      RETURN
!
      END SUBROUTINE LOCAL
!
!
! ......................................................................
! .... LOCALT ..........................................................
! ......................................................................
!     gets local coords of given traction element, iel
! ......................................................................
      SUBROUTINE LOCALT (iel, lcoordsT)
!
      IMPLICIT NONE
!
      INTEGER(ik), INTENT(IN) :: iel          ! element number
      REAL(dk), INTENT(OUT) :: lcoordsT(:,:)   ! local coords
      INTEGER(ik) :: i, i1, i2, j    ! loop variables
!
!     get local coords
      lcoordsT(1:NDIM,1:NNODELT) = COORDS(1:NDIM,ICOT(1:NNODELT,iel))
!
!     get local connectivity
      DO j = 1,NNODELT    ! traction element nodes
        i1 = NVAR*(j-1)   ! local offset
        i2 = NVAR*(ICO(j,iel)-1)  ! global offset
        DO i = 1,NVAR   ! dofs
          LJ(i+i1) = IX(i+i2)   ! map global to local
        END DO  ! dofs
      END DO  ! traction element nodes
!
      RETURN
!
      END SUBROUTINE LOCALT
!
!
! ......................................................................
! .... MAPST ...........................................................
! ......................................................................
!     maps the indices of the full stiffness matrix to packed storage
!     (for use with DPBSVX() from LAPACK, see man pages for this
!     subroutine online for an example of the storage scheme)
! ......................................................................
      SUBROUTINE MAPST (AB,S)
!
      IMPLICIT NONE
!
      REAL(dk), INTENT(INOUT) :: AB(:,:)    ! global mat (packed band storage)
      REAL(dk), INTENT(IN) :: S(:,:)        ! element mat (dense storage)
      INTEGER :: i,j,k      ! loop variables
      INTEGER :: ljr,ljc    ! global row/col connect indices
!
!$OMP PARALLEL PRIVATE(j,k,ljr,ljc)
!$OMP DO
      DO i = 1,NVEL   ! rows of element mat
!
        ljr = LJ(i)
        IF (ljr .EQ. 0) CYCLE   ! skip if node is fixed
!
        DO j = i,NVEL   ! cols of element mat
!
          ljc = LJ(j)
          IF (ljc .EQ. 0) CYCLE   ! skip if node is fixed
!
!           ensure row<=col for upper band storage
          IF (ljr .LE. ljc) THEN
            k = LBAND + 1 + ljr - ljc
!$OMP ATOMIC    ! only one thread may increment at a time
            AB(k,ljc) = AB(k,ljc) + S(i,j)
          ELSE
            k = LBAND + 1 + ljc - ljr
!$OMP ATOMIC    ! only one thread may increment at a time
            AB(k,ljr) = AB(k,ljr) + S(i,j)
          END IF
!
        END DO  ! cols
!
      END DO  ! rows
!$OMP END DO
!$OMP END PARALLEL
!
      RETURN
!
      END SUBROUTINE MAPST
!
!
! ......................................................................
! .... MAPLD ...........................................................
! ......................................................................
!     maps the indices of the element load vec to the global load vec
! ......................................................................
      SUBROUTINE MAPLD (GLO, loc, nv)
!
      IMPLICIT NONE
!
      REAL(dk), INTENT(IN) :: loc(:)    ! element vec
      INTEGER(ik), INTENT(IN) :: nv     ! # dofs
      REAL(dk), INTENT(INOUT) :: GLO(:)     ! global vec
      INTEGER(ik) :: i, ljr    ! loop variable
!
!$OMP PARALLEL PRIVATE(ljr)
!$OMP DO
      DO i = 1,nv   ! element dofs
        ljr = LJ(i)     ! row of global load vec
        IF (ljr .EQ. 0) CYCLE   ! skip if dof is fixed
!$OMP ATOMIC    ! only one thread may increment at a time
        GLO(ljr) = GLO(ljr) + loc(i)    ! increment global
      END DO  ! element dofs
!$OMP END DO
!$OMP END PARALLEL
!
      RETURN
!
      END SUBROUTINE MAPLD
!
!
! ......................................................................
! .... GLOLOC ..........................................................
! ......................................................................
!     exchange global and local stresses
! ......................................................................
      SUBROUTINE GLOLOC (SXX, SYY, SXY, SZZ, SIG, iel, gl_lo)
!
      IMPLICIT NONE
!
      LOGICAL, INTENT(IN) :: in_out   ! .TRUE.=glo->loc, .FALSE.=loc->glo
      INTEGER(ik), INTENT(IN) :: iel  ! element number
      REAL(dk), INTENT(INOUT) :: SXX(:), SYY(:), SXY(:), SZZ(:), SIG(:)  ! stress vectors
!
      IF (gl_lo) THEN
        SIG(1) = SXX(iel)
        SIG(2) = SYY(iel)
        SIG(3) = SXY(iel)
        SIG(4) = SZZ(iel)
      ELSE
        SXX(iel) = SIG(1)
        SYY(iel) = SIG(2)
        SXY(iel) = SIG(3)
        SZZ(iel) = SIG(4)
      END IF
!
      RETURN
!
      END SUBROUTINE GLOLOC
!
!
      END MODULE gcontrol