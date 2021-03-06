﻿/************************************************************************
 * PROJECT:     SlopeFEA (c) 2011 Brandon Karchewski
 *              Licensed under the Academic Free License version 3.0
 *                  http://www.opensource.org/licenses/afl-3.0.php
 * 
 * CONTACT:     Brandon Karchewski
 *              Department of Civil Engineering
 *              McMaster University, JHE-301
 *              1280 Main St W
 *              Hamilton, Ontario, Canada
 *              L8S 4L7
 *              p: 905-525-9140 x24287
 *              f: 905-529-9688
 *              e: karcheba@mcmaster.ca
 *              
 * 
 * SOURCE INFORMATION:
 * 
 * The repository for this software project is hosted on git at:
 *      
 *      git://github.com/karcheba/SlopeFEA
 *      
 * As such, the code for the project is free and open source.
 * The relevant license is AFLv3 (see link above). See the
 * README file in the root directory of the repository for a
 * detailed project description, acknowledgements, references,
 * and the revision history.
 ************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SlopeFEA
{
    public partial class SlopePlotCanvas : Canvas
    {
        private SlopeCanvas source;
        private bool panning , zooming;
        private AnalysisType analysisType;
        private double dpiX , dpiY;
        private int mouseDelta = 0;
        private Point transPoint;
        private ZoomRect zoomRect;
        private Grid xAxis , yAxis, infoBlock;
        private List<MaterialType> materialTypes;
        private FEAParams feaParams;
        private List<MaterialBlock> substructs;
        private List<fe3NodedTriElement> triElements, deformedTriMesh, stressTriMesh;
        private List<double> sxxE , syyE , sxyE , szzE , fbarE;
        private List<double> sxxN , syyN , sxyN , szzN , fbarN;
        private double minSxxE , maxSxxE , minSyyE , maxSyyE , minSxyE , maxSxyE , minSzzE , maxSzzE , minFbarE , maxFbarE;
        private double minSxxN , maxSxxN , minSyyN , maxSyyN , minSxyN , maxSxyN , minSzzN , maxSzzN , minFbarN , maxFbarN;
        private List<feNode> nodes , deformedNodes;
        private List<List<double>> disp;
        private List<DisplacementVector> dispVectors;
        private List<PlasticPoint> plasticPoints;
        private PlotModes plotMode;
        private AnalysisPhase selectedPhase;
        private double maxDisp;


        /// <summary>
        /// (Constructor) Adds various drawing Polygons.
        /// </summary>
        public SlopePlotCanvas ( SlopeCanvas source )
        {
            this.source = source;
            this.analysisType = source.AnalysisType;
            this.dpiX = source.DpiX;
            this.dpiY = source.DpiY;
            this.FilePath = source.FilePath;

            this.SizeChanged += new SizeChangedEventHandler( SlopePlotCanvas_SizeChanged );

            // For zooming to a particular area
            zoomRect = new ZoomRect();

            // Initialize material type list
            materialTypes = source.MaterialTypes;

            // Initialize list of FEA parameters
            feaParams = source.FEAParameters;

            // Initialize mesh
            substructs = new List<MaterialBlock>();
            nodes = new List<feNode>();
            deformedNodes = new List<feNode>();
            triElements = new List<fe3NodedTriElement>();
            deformedTriMesh = new List<fe3NodedTriElement>();
            stressTriMesh = new List<fe3NodedTriElement>();

            // Initialize disp vectors
            disp = new List<List<double>>();
            dispVectors = new List<DisplacementVector>();

            // Initialize plastic points
            plasticPoints = new List<PlasticPoint>();

            // Initialize stress vectors
            sxxE = new List<double>();
            syyE = new List<double>();
            sxyE = new List<double>();
            szzE = new List<double>();
            fbarE = new List<double>();
            sxxN = new List<double>();
            syyN = new List<double>();
            sxyN = new List<double>();
            szzN = new List<double>();
            fbarN = new List<double>();

            // Intialize analysis phase
            selectedPhase = source.AnalysisPhases[1];

            // Load mesh
            LoadNodeData();
            LoadElementData();
        }


        // ----------------------------------
        // PROPERTIES
        // ----------------------------------

        public string FilePath { get; set; }

        public DrawModes DrawMode { get; set; }
        public Units Units { get; set; }                        // See enum types above
        public Scales ScaleType { get; set; }

        public double OriginOffsetX { get; set; }               // Plotting origin location (in pixels)
        public double OriginOffsetY { get; set; }               // (0,0) -> BL corner

        public double Scale { get; set; }                       // Plotting scale (actual units / screen unit)
        public double Magnification { get; set; }               // Plotting magnification

        public double DpiX { get { return this.dpiX; } }
        public double DpiY { get { return this.dpiY; } }

        public double XAxisMax { get; set; }
        public double XAxisMin { get; set; }                    // Plotting axis extents (in actual units)
        public double YAxisMax { get; set; }
        public double YAxisMin { get; set; }

        public double XMajorDivision { get; set; }              // Major grid spacing (in actual units)
        public double YMajorDivision { get; set; }

        public int XMinorDivisions { get; set; }                // # minor grid divisions / major grid spacer
        public int YMinorDivisions { get; set; }

        public bool IsScaled { get; set; }                      // Toggle for initial display setup

        public PlotModes PlotMode
        {
            get { return this.plotMode; }
            set
            {
                this.plotMode = value;

                switch ( value )
                {
                    case PlotModes.DisplacementVectors:
                        PlotDisplacementVectors();
                        break;
                    case PlotModes.PlasticPoints:
                        PlotPlasticPoints();
                        break;
                    case PlotModes.SmoothedStress:
                        PlotSmoothedStress();
                        break;
                    case PlotModes.UnSmoothedStress:
                        PlotUnSmoothedStress();
                        break;
                    default:
                        PlotDeformedMesh();
                        break;
                }
            }
        }

        public AnalysisPhase SelectedPhase
        {
            get { return this.selectedPhase; }
            set
            {
                this.selectedPhase = value;
                LoadSubstructData();
                LoadNodeData();
                LoadElementData();
                this.PlotMode = this.PlotMode;  // refresh plot
            }
        }


        // ----------------------------------
        // UTILITY FUNCTIONS
        // ----------------------------------

        /// <summary>
        /// Set initial (non-painting) properties
        /// </summary>
        public void InitializeCanvas ()
        {
            this.ClipToBounds = true;

            panning = false;
            zooming = false;

            DrawMode = DrawModes.Select;
            Units = source.Units;
            ScaleType = Scales.Custom;

            OriginOffsetX = 50;
            OriginOffsetY = 50;

            XAxisMax = source.XAxisMax;
            XAxisMin = source.XAxisMin;
            YAxisMax = source.YAxisMax;
            YAxisMin = source.YAxisMin;

            XMajorDivision = source.XMajorDivision;
            YMajorDivision = source.YMajorDivision;

            XMinorDivisions = source.XMinorDivisions;
            YMinorDivisions = source.YMinorDivisions;

            IsScaled = false;
        }


        /// <summary>
        /// Construction of axes and grid
        /// </summary>
        public void BuildAxes ()
        {
            // Compute plot scaling factors [(pixels/inch) / (actual units/screen unit)]
            double xFactor = dpiX / Scale;
            double yFactor = dpiY / Scale;
            switch ( Units )
            {
                // Apply unit modification factor (screen units/inch)
                case Units.Metres: xFactor /= 0.0254; yFactor /= 0.0254; break;
                case Units.Millimetres: xFactor /= 25.4; yFactor /= 25.4; break;
                case Units.Feet: xFactor *= 12.0; yFactor *= 12.0; break;
                default: break;
            } // Result --> (pixels / actual unit)

            // Compute minor grid spacing (actual units)
            double xMinor = XMajorDivision / XMinorDivisions;
            double yMinor = YMajorDivision / YMinorDivisions;

            /*
             * Refresh X axis
             */
            xAxis.Children.Clear();

            // Add surrounding border
            Border xAxisBorder = new Border();
            xAxisBorder.BorderBrush = Brushes.DimGray;
            xAxisBorder.VerticalAlignment = VerticalAlignment.Stretch;
            xAxisBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            xAxisBorder.BorderThickness = new Thickness( 1 );
            xAxisBorder.Margin = new Thickness( 0 );
            xAxis.Children.Add( xAxisBorder );

            // Add appropriate units label
            TextBlock xLabel = new TextBlock();
            string units;
            switch ( this.Units )
            {
                case Units.Metres: units = "m"; break;
                case Units.Millimetres: units = "mm"; break;
                case Units.Feet: units = "ft"; break;
                default: units = "in"; break;
            }
            xLabel.Text = string.Format( "X ({0})" , units );
            xLabel.FontSize = 10;
            xLabel.VerticalAlignment = VerticalAlignment.Bottom;
            xLabel.HorizontalAlignment = HorizontalAlignment.Left;
            xLabel.Margin = new Thickness( 10 , 0 , 0 , 7 );
            xAxis.Children.Add( xLabel );

            Line majorLine , minorLine;      // For boxing axis lines and labels
            TextBlock majorLabel;

            // Add major and minor axis lines and labels to major divisions
            for ( double xMajor = XAxisMin ; xMajor <= XAxisMax ; xMajor += XMajorDivision )
            {
                majorLine = new Line();
                majorLine.Stroke = Brushes.Black;
                majorLine.X1 = majorLine.X2 = xMajor * xFactor + OriginOffsetX;
                majorLine.Y1 = 0;
                majorLine.Y2 = 25;
                xAxis.Children.Add( majorLine );

                majorLabel = new TextBlock();
                majorLabel.Text = string.Format( "{0}" , Math.Round( xMajor , 2 ) );
                majorLabel.FontSize = 10;
                majorLabel.VerticalAlignment = VerticalAlignment.Top;
                majorLabel.HorizontalAlignment = HorizontalAlignment.Left;
                majorLabel.Margin = new Thickness( majorLine.X1 + 5 , 13 , 0 , 0 );
                xAxis.Children.Add( majorLabel );

                if ( Math.Abs( xMajor - this.XAxisMax ) > 1e-3 )
                {
                    for ( int ixMinor = 1 ; ixMinor < XMinorDivisions ; ixMinor++ )
                    {
                        minorLine = new Line();
                        minorLine.Stroke = Brushes.Black;
                        minorLine.X1 = minorLine.X2 = majorLine.X1 + ixMinor * xMinor * xFactor;
                        minorLine.Y1 = 0;
                        minorLine.Y2 = 10;
                        xAxis.Children.Add( minorLine );
                    }
                }
            }

            /*
             * Refresh Y axis
             */
            yAxis.Children.Clear();

            // Add surrounding border
            Border yAxisBorder = new Border();
            yAxisBorder.BorderBrush = Brushes.DimGray;
            yAxisBorder.VerticalAlignment = VerticalAlignment.Stretch;
            yAxisBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            yAxisBorder.BorderThickness = new Thickness( 1 );
            yAxisBorder.Margin = new Thickness( 0 );
            yAxis.Children.Add( yAxisBorder );

            // Add appropriate units label
            TextBlock yLabel = new TextBlock();
            yLabel.Text = string.Format( "Y\n({0})" , units );
            yLabel.FontSize = 10;
            yLabel.TextAlignment = TextAlignment.Right;
            yLabel.VerticalAlignment = VerticalAlignment.Bottom;
            yLabel.HorizontalAlignment = HorizontalAlignment.Right;
            yLabel.Margin = new Thickness( 0 , 0 , 35 , 10 );
            yAxis.Children.Add( yLabel );

            // Add major and minor axis lines and labels to major divisions
            // (NB: Actual units origin @ (0, ActualHeight) which is BL corner
            //      while screen pixels origin @ (0,0) which is TL corner
            for ( double yMajor = YAxisMin ; yMajor <= YAxisMax ; yMajor += YMajorDivision )
            {
                majorLine = new Line();
                majorLine.Stroke = Brushes.Black;
                majorLine.Y1 = majorLine.Y2 = ActualHeight - (yMajor * yFactor + OriginOffsetY);
                majorLine.X1 = yAxis.ActualWidth;
                majorLine.X2 = majorLine.X1 - 25;
                yAxis.Children.Add( majorLine );

                majorLabel = new TextBlock();
                majorLabel.Text = string.Format( "{0}" , Math.Round( yMajor , 2 ) );
                majorLabel.FontSize = 10;
                majorLabel.VerticalAlignment = VerticalAlignment.Bottom;
                majorLabel.HorizontalAlignment = HorizontalAlignment.Right;
                majorLabel.TextAlignment = TextAlignment.Right;
                majorLabel.Margin = new Thickness( 0 , 0 , 15 , ActualHeight - majorLine.Y1 + 3 );
                yAxis.Children.Add( majorLabel );

                if ( Math.Abs( yMajor - YAxisMax ) > 1e-3 )
                {
                    for ( int iyMinor = 1 ; iyMinor < YMinorDivisions ; iyMinor++ )
                    {
                        minorLine = new Line();
                        minorLine.Stroke = Brushes.Black;
                        minorLine.Y1 = minorLine.Y2 = majorLine.Y1 - iyMinor * yMinor * yFactor;
                        minorLine.X1 = yAxis.ActualWidth;
                        minorLine.X2 = minorLine.X1 - 10;
                        yAxis.Children.Add( minorLine );
                    }
                }
            }

            /*
             * Refresh magnification label
             */
            TextBlock plotMag = (TextBlock) ((Grid) ((Grid) this.Parent).Children[2]).Children[7];
            plotMag.Text = "Magnification: " + Math.Round( Magnification , 2 );

            /*
             * Refresh drawing canvas
             */
            this.Children.Clear();

            // Add substructs first ...
            substructs.ForEach( delegate( MaterialBlock mb ) { this.Children.Add( mb.Boundary ); } );

            // ... then add deformed elements on top of substructs ...
            deformedTriMesh.ForEach(
                delegate( fe3NodedTriElement element ) { this.Children.Add( element.Boundary ); } );

            // ... then add displacement vectors on top of deformed mesh (these will never be present simultaneously) ...
            dispVectors.ForEach(
                delegate( DisplacementVector dv ) { dv.PlotLines.ForEach( delegate( Polyline l ) { this.Children.Add( l ); } ); } );

            // ... then add plastic points on top of disp vectors (these will never be present simultaneously) ...
            plasticPoints.ForEach(
                delegate( PlasticPoint pp ) { this.Children.Add( pp.Display ); } );

            // ... then stress plot elements on top of plastic points (these will never be present simultaneously) ...
            stressTriMesh.ForEach(
                delegate( fe3NodedTriElement element ) { this.Children.Add( element.Boundary ); } );

            // ... then add temporary drawing objects on top of everything
            this.Children.Add( zoomRect.Boundary );
        }


        /// <summary>
        /// Apply translation to canvas and axes content
        /// </summary>
        public void Translate ( Vector delta )
        {
            /*
             * Update plotting origin (in pixels)
             */
            OriginOffsetX += delta.X;
            OriginOffsetY -= delta.Y;


            /*
             * Translate axis components
             */

            Line line;          // For unboxing axis lines and labels
            TextBlock tb;

            // Update coordinates of x axis objects
            for ( int i = 2 ; i < xAxis.Children.Count ; i++ )
            {
                if ( xAxis.Children[i] is Line )
                {
                    line = xAxis.Children[i] as Line;
                    line.X1 = line.X2 += delta.X;
                }
                else if ( xAxis.Children[i] is TextBlock )
                {
                    tb = xAxis.Children[i] as TextBlock;
                    tb.Margin = new Thickness( tb.Margin.Left + delta.X , tb.Margin.Top , 0 , 0 );
                }
            }

            // Update coordinates of y axis objects
            for ( int i = 2 ; i < yAxis.Children.Count ; i++ )
            {
                if ( yAxis.Children[i] is Line )
                {
                    line = yAxis.Children[i] as Line;
                    line.Y1 = line.Y2 += delta.Y;
                }
                else if ( yAxis.Children[i] is TextBlock )
                {
                    tb = yAxis.Children[i] as TextBlock;
                    tb.Margin = new Thickness( 0 , 0 , tb.Margin.Right , tb.Margin.Bottom - delta.Y );
                }
            }


            /*
             * Translate plotting canvas components
             */

            // Update substructs
            substructs.ForEach( delegate( MaterialBlock mb ) { mb.Translate( delta ); } );
            
            // Update FEA elements
            deformedTriMesh.ForEach( delegate( fe3NodedTriElement element ) { element.Translate( delta ); } );

            // Update disp vectors
            dispVectors.ForEach( delegate( DisplacementVector dv ) { dv.Translate( delta ); } );

            // Update plastic points
            plasticPoints.ForEach( delegate( PlasticPoint pp ) { pp.Translate( delta ); } );

            // Update stress plot
            stressTriMesh.ForEach( delegate( fe3NodedTriElement element ) { element.Translate( delta ); } );
        }


        /// <summary>
        /// Zooms canvas content and axes
        /// </summary>
        /// <param name="factor">scaling factor for zoom</param>
        /// <param name="centre">central reference point</param>
        public void Zoom ( double factor , Point centre )
        {
            // Update plotting scale
            Scale /= factor;

            // Update scale display
            TextBlock plotScale = (TextBlock) ((Grid) ((Grid) this.Parent).Children[2]).Children[6];
            plotScale.Text = "Scale: " + Math.Round( Scale , 2 ) + ":1";

            // Update plotting origin (in pixels)
            OriginOffsetX = centre.X + factor * (OriginOffsetX - centre.X);
            OriginOffsetY = (ActualHeight - centre.Y) - factor * ((ActualHeight - OriginOffsetY) - centre.Y);


            /*
             * Zoom plotting axes
             */

            Line line;          // For unboxing axis lines and labels
            TextBlock tb;

            // Zoom x axis components
            for ( int i = 2 ; i < xAxis.Children.Count ; i++ )
            {
                if ( xAxis.Children[i] is Line )
                {
                    line = xAxis.Children[i] as Line;
                    line.X1 = line.X2 = centre.X + factor * (line.X2 - centre.X);
                }
                else if ( xAxis.Children[i] is TextBlock )
                {
                    tb = xAxis.Children[i] as TextBlock;
                    tb.Margin = new Thickness( centre.X + factor * (tb.Margin.Left - centre.X) , tb.Margin.Top , 0 , 0 );
                }
            }

            // Zoom y axis components
            for ( int i = 2 ; i < yAxis.Children.Count ; i++ )
            {
                if ( yAxis.Children[i] is Line )
                {
                    line = yAxis.Children[i] as Line;
                    line.Y1 = line.Y2 = centre.Y + factor * (line.Y2 - centre.Y);
                }
                else if ( yAxis.Children[i] is TextBlock )
                {
                    tb = yAxis.Children[i] as TextBlock;
                    tb.Margin = new Thickness( 0 , 0 , tb.Margin.Right , (yAxis.ActualHeight - centre.Y) - factor * (yAxis.ActualHeight - tb.Margin.Bottom - centre.Y) );
                }
            }


            /*
             * Zoom canvas plotting components
             */

            // Zoom substructs
            substructs.ForEach( delegate( MaterialBlock mb ) { mb.Zoom( factor , centre ); } );

            // Zoom FEA elements
            deformedTriMesh.ForEach( delegate( fe3NodedTriElement element ) { element.Zoom( factor , centre ); } );

            // Zoom disp vectors
            dispVectors.ForEach( delegate( DisplacementVector dv ) { dv.Zoom( factor , centre ); } );

            // Zoom plastic points
            plasticPoints.ForEach( delegate( PlasticPoint pp ) { pp.Zoom( factor , centre ); } );

            // Zoom stress plot
            stressTriMesh.ForEach( delegate( fe3NodedTriElement element ) { element.Zoom( factor , centre ); } );
        }


        /// <summary>
        /// Fits boundaries and axes extents to view window
        /// (Essentially a wrapper for Translate and Zoom with computation of appropriate
        /// translation and scaling factors)
        /// </summary>
        /// <param name="zoom">true = centre view AND fit extents, false = just centre view</param>
        public void CentreAndFitExtents ( bool zoom )
        {
            // Get initial values for extents from plotting axes
            // (NB: Drawing is allowed outside this range and this
            //      will also be checked)
            double xMax = XAxisMax;
            double xMin = XAxisMin;
            double yMax = YAxisMax;
            double yMin = YAxisMin;

            // Get units dependent scaling factor
            double factor;
            switch ( Units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            // Convert maximum extents to pixels
            double xMaxPix = (xMax / Scale) * (dpiX / factor) + OriginOffsetX;
            double xMinPix = (xMin / Scale) * (dpiX / factor) + OriginOffsetX;
            double yMaxPix = ActualHeight - ((yMax / Scale) * (dpiY / factor) + OriginOffsetY);
            double yMinPix = ActualHeight - ((yMin / Scale) * (dpiY / factor) + OriginOffsetY);

            // Compute centre of desired window and centre of current window
            Point fitCentre = new Point( 0.5 * (xMaxPix + xMinPix) , 0.5 * (yMaxPix + yMinPix) );
            Point canvCentre = new Point( 0.5 * ActualWidth , 0.5 * ActualHeight );

            // Shift points to desired centre
            Translate( canvCentre - fitCentre );

            if ( zoom )
            {
                // Compute desired scale (fit all content with minimum of 75 pixels of padding)
                double canvasWidth = (ActualWidth - 75) / dpiX * factor;
                double canvasHeight = (ActualHeight - 75) / dpiY * factor;
                double reqScale = Math.Max( (xMax - xMin) / canvasWidth , (yMax - yMin) / canvasHeight );

                // Zoom by factor to achieve required scale with centre of window as focus point
                Zoom( Scale / reqScale , new Point( 0.5 * ActualWidth , 0.5 * ActualHeight ) );
            }
        }


        /// <summary>
        /// Zooms the window to fit the rectangle specified by zoomBounds
        /// </summary>
        public void ZoomArea ( Polygon zoomBounds )
        {
            // Compute centre of zoom region and centre of canvas
            Point fitCentre =
                new Point( 0.5 * (zoomBounds.Points[0].X + zoomBounds.Points[2].X) ,
                            0.5 * (zoomBounds.Points[0].Y + zoomBounds.Points[2].Y) );

            Point canvCentre = new Point( 0.5 * ActualWidth , 0.5 * ActualHeight );

            // Shift points so centres are coincident
            Translate( canvCentre - fitCentre );

            // Get units dependent scaling factor
            double factor;
            switch ( Units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            // Compute zoom region dimensions (in actual units)
            double realWidth = Math.Abs( zoomBounds.Points[0].X - zoomBounds.Points[2].X ) * (factor / dpiX) * Scale;
            double realHeight = Math.Abs( zoomBounds.Points[0].Y - zoomBounds.Points[2].Y ) * (factor / dpiY) * Scale;

            // Compute canvas size (in screen units)
            double canvasWidth = ActualWidth / dpiX * factor;
            double canvasHeight = ActualHeight / dpiY * factor;

            // Compute required scale (actual units/screen units)
            double reqScale = Math.Max( realWidth / canvasWidth , realHeight / canvasHeight );

            // Zoom by appropriate factor centred on canvas centre
            Zoom( Scale / reqScale , new Point( 0.5 * ActualWidth , 0.5 * ActualHeight ) );
        }


        /// <summary>
        /// Cancels current drawing operation
        /// </summary>
        public void CancelDrawing ()
        {
            panning = false;
            zooming = false;
        }


        /// <summary>
        /// Gets substruct (material block data) from file
        /// </summary>
        public void LoadSubstructData ()
        {
            if ( !File.Exists( FilePath ) )
            {
                MessageBox.Show( "Could not find input data file." , "Error" );
                return;
            }

            // get canvas dimensions/properties
            double originX = OriginOffsetX ,
                   originY = OriginOffsetY ,
                   scale = Scale ,
                   yHeight = ActualHeight;
            Units units = Units;

            // get units dependent scaling factor
            double factor;
            switch ( units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            // Load material blocks from source canvas
            substructs = new List<MaterialBlock>();
            using ( TextReader tr = new StreamReader( FilePath ) )
            {
                // advance to material block data
                while ( !tr.ReadLine().Contains( "MATERIAL BLOCK DATA" ) ) ;

                tr.ReadLine();
                tr.ReadLine();

                int numMaterialBlocks = int.Parse( tr.ReadLine().Split( '=' )[1] );

                tr.ReadLine();

                if ( numMaterialBlocks > 0 )
                {
                    MaterialBlock newMaterialBlock;
                    MaterialType newMaterialType;
                    Point[] materialBoundPoints;
                    string materialName;
                    int numMaterialBoundPoints , numLineConstraints , numLineLoads , numPointLoads;
                    double xCoord , yCoord;
                    string[] coords;

                    for ( int i = 0 ; i < numMaterialBlocks ; i++ )
                    {
                        tr.ReadLine();
                        tr.ReadLine();

                        numMaterialBoundPoints = int.Parse( tr.ReadLine().Split( '=' )[1] );

                        materialBoundPoints = new Point[numMaterialBoundPoints];

                        for ( int j = 0 ; j < numMaterialBoundPoints ; j++ )
                        {
                            coords = tr.ReadLine().Split( new char[] { ',' , ' ' } , StringSplitOptions.RemoveEmptyEntries );
                            xCoord = double.Parse( coords[0] );
                            yCoord = double.Parse( coords[1] );
                            materialBoundPoints[j].X = xCoord / (factor * Scale) * dpiX + OriginOffsetX;
                            materialBoundPoints[j].Y = ActualHeight - (yCoord / (factor * Scale) * dpiY + OriginOffsetY);
                        }

                        newMaterialBlock = new MaterialBlock( this , null , materialBoundPoints );

                        numLineConstraints = int.Parse( tr.ReadLine().Split( '=' )[1] );
                        for ( int j = 0 ; j < numLineConstraints ; j++ ) tr.ReadLine();

                        numLineLoads = int.Parse( tr.ReadLine().Split( '=' )[1] );
                        for ( int j = 0 ; j < numLineLoads ; j++ ) tr.ReadLine();

                        numPointLoads = int.Parse( tr.ReadLine().Split( '=' )[1] );
                        for ( int j = 0 ; j < numPointLoads ; j++ ) tr.ReadLine();

                        substructs.Add( newMaterialBlock );

                        tr.ReadLine();
                    }


                    // advance to phase data
                    while ( !tr.ReadLine().Contains( "ANALYSIS PHASES" ) ) ;
                    int phaseNum = SelectedPhase.Number;
                    string phaseHeader = string.Format( "Phase #{0}" , phaseNum );
                    while ( !tr.ReadLine().Contains( phaseHeader ) ) ;
                    while ( !tr.ReadLine().Contains( "Material Block #" ) ) ;
                    
                    // get material type info
                    string line;
                    for ( int i = 0 ; i < numMaterialBlocks ; i++ )
                    {
                        materialName = tr.ReadLine();
                        newMaterialType = materialTypes.Find( delegate( MaterialType mt ) { return mt.Name == materialName; } );
                        substructs[i].Material = newMaterialType;

                        while ( (line = tr.ReadLine()) != null ) if ( line.Contains( "Material Block #" ) ) break;
                    }
                }
            }
        }


        /// <summary>
        /// Gets node data from output file
        /// </summary>
        public void LoadNodeData ()
        {
            string[] pathSplit = FilePath.Split( '.' );

            string phaseNum = SelectedPhase.Number.ToString();
            while ( phaseNum.Length < 3 ) phaseNum = "0" + phaseNum;

            // Find node file
            pathSplit[1] = "ND" + phaseNum;
            string nodePath = string.Join( "." , pathSplit );
            if ( !File.Exists( nodePath ) )
            {
                MessageBox.Show( "Could not find node data file." , "Error" );
                return;
            }

            // Load node data from file
            // N.B.: nodes list is initialized with a null to 
            //          account for 1 based indexing in Fortran
            nodes.Clear(); nodes.Add( null );
            disp.Clear(); disp.Add( null );
            sxxN.Clear(); syyN.Clear(); sxyN.Clear(); szzN.Clear(); fbarN.Clear();
            sxxN.Add( 0 ); syyN.Add( 0 ); sxyN.Add( 0 ); szzN.Add( 0 ); fbarN.Add( 0 );
            maxDisp = 0.0;
            double currDisp;
            int numPhases = source.AnalysisPhases.Count - 1;
            using ( TextReader tr = new StreamReader( nodePath ) )
            {
                // advance to line containing data
                tr.ReadLine();  //
                tr.ReadLine();  //
                tr.ReadLine();  // INOD         COORDS (X,Y...) ....
                tr.ReadLine();  //

                string line;
                string[] lineSplit;
                int inod;
                while ( (line = tr.ReadLine() ) != null )
                {
                    lineSplit = line.Split( new char[] { ' ' , '\t' } , StringSplitOptions.RemoveEmptyEntries );

                    if ( lineSplit[0][0] == '#' ) continue;     // skip comment lines

                    inod = int.Parse( lineSplit[0] );
                    nodes.Add( new feNode( inod , false ,
                                            double.Parse( lineSplit[1] ) ,
                                            double.Parse( lineSplit[2] ) ,
                                            numPhases ) );

                    disp.Add( new List<double>(){   double.Parse(lineSplit[3]),
                                                    double.Parse(lineSplit[4])} );

                    currDisp = Math.Sqrt( Math.Pow( disp[inod][0] , 2 ) + Math.Pow( disp[inod][1] , 2 ) );
                    if ( currDisp > maxDisp ) maxDisp = currDisp;

                    sxxN.Add( double.Parse( lineSplit[5] ) );
                    syyN.Add( double.Parse( lineSplit[6] ) );
                    sxyN.Add( double.Parse( lineSplit[7] ) );
                    szzN.Add( double.Parse( lineSplit[8] ) );
                    fbarN.Add( double.Parse( lineSplit[9] ) );
                }
            }

            minSxxN = minSyyN = minSxyN = minSzzN = minFbarN = double.MaxValue;
            maxSxxN = maxSyyN = maxSxyN = maxSzzN = maxFbarN = double.MinValue;
            for ( int i = 1 ; i < sxxN.Count ; i++ )
            {
                if ( sxxN[i] < minSxxN ) minSxxN = sxxN[i];
                if ( sxxN[i] > maxSxxN ) maxSxxN = sxxN[i];

                if ( syyN[i] < minSyyN ) minSyyN = syyN[i];
                if ( syyN[i] > maxSyyN ) maxSyyN = syyN[i];

                if ( sxyN[i] < minSxyN ) minSxyN = sxyN[i];
                if ( sxyN[i] > maxSxyN ) maxSxyN = sxyN[i];

                if ( szzN[i] < minSzzN ) minSzzN = szzN[i];
                if ( szzN[i] > maxSzzN ) maxSzzN = szzN[i];

                if ( fbarN[i] < minFbarN ) minFbarN = fbarN[i];
                if ( fbarN[i] > maxFbarN ) maxFbarN = fbarN[i];
            }
        }


        /// <summary>
        /// Gets element data from output file
        /// </summary>
        public void LoadElementData ()
        {
            string[] pathSplit = FilePath.Split( '.' );

            string phaseNum = SelectedPhase.Number.ToString();
            while ( phaseNum.Length < 3 ) phaseNum = "0" + phaseNum;

            // Find node file
            pathSplit[1] = "EL" + phaseNum;
            string elementPath = string.Join( "." , pathSplit );
            if ( !File.Exists( elementPath ) )
            {
                MessageBox.Show( "Could not find element data file." , "Error" );
                return;
            }

            // Load element data from file
            triElements.Clear();
            sxxE.Clear(); syyE.Clear(); sxyE.Clear(); szzE.Clear(); fbarE.Clear();
            int numPhases = source.AnalysisPhases.Count - 1;
            using ( TextReader tr = new StreamReader( elementPath ) )
            {
                //int nel = int.Parse( tr.ReadLine().Split( '\t' )[0] );

                // advance to line containing data
                tr.ReadLine();  //
                tr.ReadLine();  //
                tr.ReadLine();  // IEL         ICO ....
                tr.ReadLine();  //

                string line;
                string[] lineSplit;
                int materialIndex;
                while ( (line = tr.ReadLine()) != null )
                {
                    lineSplit = line.Split( new char[] { ' ' , '\t' } , StringSplitOptions.RemoveEmptyEntries );

                    if ( lineSplit[0][0] == '#' ) continue;     // skip comment lines

                    materialIndex = int.Parse( lineSplit[4] ) - 1;
                    if ( materialIndex < 0 ) continue;

                    triElements.Add( new fe3NodedTriElement( int.Parse( lineSplit[0] ) ,
                        nodes[int.Parse( lineSplit[1] )] ,
                        nodes[int.Parse( lineSplit[2] )] ,
                        nodes[int.Parse( lineSplit[3] )] ,
                        int.Parse( lineSplit[5] ) == 1 , int.Parse( lineSplit[6] ) == 1 ,
                        materialTypes[materialIndex] ,
                        false ) );

                    sxxE.Add( double.Parse( lineSplit[7] ) );
                    syyE.Add( double.Parse( lineSplit[8] ) );
                    sxyE.Add( double.Parse( lineSplit[9] ) );
                    szzE.Add( double.Parse( lineSplit[10] ) );
                    fbarE.Add( double.Parse( lineSplit[11] ) );
                }
            }

            minSxxE = minSyyE = minSxyE = minSzzE = minFbarE = double.MaxValue;
            maxSxxE = maxSyyE = maxSxyE = maxSzzE = maxFbarE = double.MinValue;
            for ( int i = 0 ; i < sxxE.Count ; i++ )
            {
                if ( sxxE[i] < minSxxE ) minSxxE = sxxE[i];
                if ( sxxE[i] > maxSxxE ) maxSxxE = sxxE[i];

                if ( syyE[i] < minSyyE ) minSyyE = syyE[i];
                if ( syyE[i] > maxSyyE ) maxSyyE = syyE[i];

                if ( sxyE[i] < minSxyE ) minSxyE = sxyE[i];
                if ( sxyE[i] > maxSxyE ) maxSxyE = sxyE[i];

                if ( szzE[i] < minSzzE ) minSzzE = szzE[i];
                if ( szzE[i] > maxSzzE ) maxSzzE = szzE[i];

                if ( fbarE[i] < minFbarE ) minFbarE = fbarE[i];
                if ( fbarE[i] > maxFbarE ) maxFbarE = fbarE[i];
            }
        }

        public void ClearPlots ()
        {
            deformedTriMesh.Clear();
            dispVectors.Clear();
            plasticPoints.Clear();
            stressTriMesh.Clear();
        }

        /// <summary>
        /// Plots deformed mesh output
        /// </summary>
        public void PlotDeformedMesh ( bool autoMag = true )
        {
            // clear existing plots
            ClearPlots();

            // get canvas dimensions/properties
            double originX = OriginOffsetX ,
                   originY = OriginOffsetY ,
                   scale = Scale ,
                   yHeight = ActualHeight;
            Units units = Units;

            // get units dependent scaling factor
            double factor;
            switch ( units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            Polygon newPolygon;
            double x , y;

            // compute magnification
            if ( autoMag ) Magnification = 0.25 * 0.5 * (feaParams.RowHeight + feaParams.ColWidth) / maxDisp;

            // compute deformed coordinates
            deformedNodes.Clear(); deformedNodes.Add( null );
            int numPhases = source.AnalysisPhases.Count - 1;
            for ( int i = 1 ; i < nodes.Count ; i++ )
            {
                deformedNodes.Add( new feNode( i , false ,
                    nodes[i].X + Magnification * disp[i][0] ,
                    nodes[i].Y + Magnification * disp[i][1] ,
                    numPhases ) );
            }

            // set mesh to deformed coords
            fe3NodedTriElement newElement;
            foreach ( fe3NodedTriElement element in triElements )
            {
                if ( element.Material == null ) continue;    // skip inactive elements

                newElement = new fe3NodedTriElement( element.Number ,
                    deformedNodes[element.Nodes[0].Number] ,
                    deformedNodes[element.Nodes[1].Number] ,
                    deformedNodes[element.Nodes[2].Number] ,
                    false , false ,
                    element.Material , false );

                newPolygon = new Polygon();
                newPolygon.StrokeThickness = 0.8;
                newPolygon.Stroke = Brushes.Red;
                newPolygon.Opacity = /*1.0*/ 0.8;
                newPolygon.Fill = /*Brushes.White*/ element.Material.Fill;

                foreach ( feNode node in newElement.Nodes )
                {
                    x = node.X / (scale * factor) * dpiX + originX;
                    y = yHeight - (node.Y / (scale * factor) * dpiY + originY);
                    newPolygon.Points.Add( new Point( x , y ) );
                }

                newElement.Boundary = newPolygon;

                deformedTriMesh.Add( newElement );
            }

            // refresh plotting axes
            BuildAxes();
        }


        /// <summary>
        /// Plots a vector at each node indicating direction
        /// and relative magnitude of displacement
        /// </summary>
        public void PlotDisplacementVectors ( bool autoMag = true )
        {
            // clear existing plots
            ClearPlots();

            // get canvas dimensions/properties
            double originX = OriginOffsetX ,
                   originY = OriginOffsetY ,
                   scale = Scale ,
                   yHeight = ActualHeight;
            Units units = Units;

            // get units dependent scaling factor
            double factor;
            switch ( units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            double x , y;

            // compute magnification
            if ( autoMag ) Magnification = 0.5 * 0.5 * (feaParams.RowHeight + feaParams.ColWidth) / maxDisp;

            // plot the disp vectors
            for ( int i = 1 ; i < nodes.Count ; i++ )
            {
                x = nodes[i].X / (scale * factor) * dpiX + originX;
                y = yHeight - (nodes[i].Y / (scale * factor) * dpiY + originY);
                dispVectors.Add( new DisplacementVector( this , new Point( x , y ) , disp[i] ) );
            }

            BuildAxes();
        }


        /// <summary>
        /// Plots an indicator at each plastic point
        /// </summary>
        public void PlotPlasticPoints ()
        {
            // clear existing plots
            ClearPlots();

            // get canvas dimensions/properties
            double originX = OriginOffsetX ,
                   originY = OriginOffsetY ,
                   scale = Scale ,
                   yHeight = ActualHeight;
            Units units = Units;

            // get units dependent scaling factor
            double factor;
            switch ( units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            double x , y;

            // plot the plastic points
            foreach(fe3NodedTriElement element in triElements)
            {
                if ( !(element.IsPlastic || element.IsTensile) ) continue;

                x = element.Centroid.X / (scale * factor) * dpiX + originX;
                y = yHeight - (element.Centroid.Y / (scale * factor) * dpiY + originY);

                plasticPoints.Add( new PlasticPoint( new Point( x , y ) , element.IsPlastic , element.IsTensile ) );
            }

            BuildAxes();
        }


        /// <summary>
        /// Plots colour scaled contours based on polynomial
        /// smoothed stresses at each node.
        /// </summary>
        public void PlotSmoothedStress ()
        {
            // clear existing plots
            ClearPlots();

            // get canvas dimensions/properties
            double originX = OriginOffsetX ,
                   originY = OriginOffsetY ,
                   scale = Scale ,
                   yHeight = ActualHeight;
            Units units = Units;

            // get units dependent scaling factor
            double factor;
            switch ( units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            // set plot type
            MenuItem smoothedMenu = (MenuItem) ((MenuItem) ((Menu) ((DockPanel) ((Grid) ((Grid) this.Parent).Parent).Children[0]).Children[0]).Items[1]).Items[3];
            int plotType = 0;
            for ( int i = 0 ; i < smoothedMenu.Items.Count ; i++ )
            {
                if ( ((MenuItem) smoothedMenu.Items[i]).IsChecked )
                {
                    plotType = i;
                    break;
                }
            }

            // load appropriate stress values
            List<double> stress;
            double minVal , maxVal;
            switch ( plotType )
            {
                case 0: // XX
                    stress = sxxN;
                    maxVal = minSxxN;
                    minVal = maxSxxN;
                    break;
                case 1: // YY
                    stress = syyN;
                    maxVal = minSyyN;
                    minVal = maxSyyN;
                    break;
                case 2: // XY
                    stress = sxyN;
                    maxVal = minSxyN;
                    minVal = maxSxyN;
                    break;
                case 3: // ZZ
                    stress = szzN;
                    maxVal = minSzzN;
                    minVal = maxSzzN;
                    break;
                default: // FBAR
                    stress = fbarN;
                    minVal = minFbarN;
                    maxVal = maxFbarN;
                    break;
            }

            // compute key values for colour scale
            double diff = maxVal - minVal;
            double qdiff = 0.25 * diff;
            double lowQVal = minVal + 0.25 * diff;
            double midVal = minVal + 0.5 * diff;
            double uppQVal = minVal + 0.75 * diff;


            // set mesh to regular coords and
            // create fill colours based on stress
            // at integration points
            Polygon newPolygon;
            double x , y;
            fe3NodedTriElement newElement;
            byte r , g , b;
            double st;
            double x1 , y1 , x2 , y2 , x3 , y3 ,
                s1 , s2 , s3 ,
                x_min , x_max , x_ymin , y_min , y_xmax, s_ymin , s_xymin , s_xmax ,
                dsdx , dsdy , dydx , detA , detA1 , detA2;
            for ( int i = 0 ; i < triElements.Count ; i++ )
            {
                if ( triElements[i].Material == null ) continue;    // skip inactive elements

                newElement = new fe3NodedTriElement( triElements[i].Number ,
                    nodes[triElements[i].Nodes[0].Number] ,
                    nodes[triElements[i].Nodes[1].Number] ,
                    nodes[triElements[i].Nodes[2].Number] ,
                    false , false ,
                    null , false );

                newPolygon = new Polygon();
                newPolygon.StrokeThickness = 0;
                newPolygon.Stroke = Brushes.Transparent;
                newPolygon.Opacity = 1.0;

                // compute linear gradient brush values
                x_min = double.MaxValue; x_max = double.MinValue; y_min = double.MaxValue;
                x_ymin = 0; s_ymin = 0;
                foreach ( feNode node in newElement.Nodes )
                {
                    if ( node.X < x_min ) x_min = node.X;
                    if ( node.X > x_max ) x_max = node.X;
                    if ( node.Y < y_min )
                    {
                        y_min = node.Y;
                        x_ymin = node.X;
                        s_ymin = stress[node.Number];
                    }

                    x = node.X / (scale * factor) * dpiX + originX;
                    y = yHeight - (node.Y / (scale * factor) * dpiY + originY);
                    newPolygon.Points.Add( new Point( x , y ) );
                }

                x1 = newElement.Nodes[0].X;
                y1 = newElement.Nodes[0].Y;
                s1 = stress[newElement.Nodes[0].Number];
                x2 = newElement.Nodes[1].X;
                y2 = newElement.Nodes[1].Y;
                s2 = stress[newElement.Nodes[1].Number];
                x3 = newElement.Nodes[2].X;
                y3 = newElement.Nodes[2].Y;
                s3 = stress[newElement.Nodes[2].Number];

                detA = (x2 * y3 - x3 * y2) - (x1 * y3 - x3 * y1) + (x1 * y2 - x2 * y1);
                detA1 = (s2 * y3 - s3 * y2) - (s1 * y3 - s3 * y1) + (s1 * y2 - s2 * y1);
                detA2 = (x2 * s3 - x3 * s2) - (x1 * s3 - x3 * s1) + (x1 * s2 - x2 * s1);
                dsdx = detA1 / detA;
                dsdy = detA2 / detA;
                dydx = dsdx / dsdy;
                s_xymin = s_ymin - dsdx * (x_ymin - x_min);
                y_xmax = y_min + dydx * (x_max - x_min);
                s_xmax = s_xymin + dsdx * (x_max - x_min) + dsdy * (y_xmax - y_min);

                // compute fill colour
                st = s_xymin;
                if ( plotType == 4 )
                {
                    if ( st <= minVal )
                    {
                        r = 0;
                        g = 0;
                        b = 255;
                    }
                    else if ( st < lowQVal )
                    {
                        r = 0;
                        g = (byte) (255 * (st - minVal) / qdiff);
                        b = 255;
                    }
                    else if ( st < midVal )
                    {
                        r = 0;
                        g = 255;
                        b = (byte) (255 * (1 - (st - lowQVal) / qdiff));
                    }
                    else if ( st < uppQVal )
                    {
                        r = (byte) (255 * (st - midVal) / qdiff);
                        g = 255;
                        b = 0;
                    }
                    else if ( st < maxVal )
                    {
                        r = 255;
                        g = (byte) (255 * (1 - (st - uppQVal) / qdiff));
                        b = 0;
                    }
                    else
                    {
                        r = 255;
                        g = 0;
                        b = 0;
                    }
                }
                else
                {
                    if ( st >= minVal )
                    {
                        r = 0;
                        g = 0;
                        b = 255;
                    }
                    else if ( st > lowQVal )
                    {
                        r = 0;
                        g = (byte) (255 * (st - minVal) / qdiff);
                        b = 255;
                    }
                    else if ( st > midVal )
                    {
                        r = 0;
                        g = 255;
                        b = (byte) (255 * (1 - (st - lowQVal) / qdiff));
                    }
                    else if ( st > uppQVal )
                    {
                        r = (byte) (255 * (st - midVal) / qdiff);
                        g = 255;
                        b = 0;
                    }
                    else if ( st > maxVal )
                    {
                        r = 255;
                        g = (byte) (255 * (1 - (st - uppQVal) / qdiff));
                        b = 0;
                    }
                    else
                    {
                        r = 255;
                        g = 0;
                        b = 0;
                    }
                }
                Color startColour = Color.FromRgb( r , g , b );
                Point startPoint = new Point( x_min / (scale * factor) * dpiX + originX ,
                    y = yHeight - (y_min / (scale * factor) * dpiY + originY) );

                st = s_xmax;
                if ( plotType == 4 )
                {
                    if ( st <= minVal )
                    {
                        r = 0;
                        g = 0;
                        b = 255;
                    }
                    else if ( st < lowQVal )
                    {
                        r = 0;
                        g = (byte) (255 * (st - minVal) / qdiff);
                        b = 255;
                    }
                    else if ( st < midVal )
                    {
                        r = 0;
                        g = 255;
                        b = (byte) (255 * (1 - (st - lowQVal) / qdiff));
                    }
                    else if ( st < uppQVal )
                    {
                        r = (byte) (255 * (st - midVal) / qdiff);
                        g = 255;
                        b = 0;
                    }
                    else if ( st < maxVal )
                    {
                        r = 255;
                        g = (byte) (255 * (1 - (st - uppQVal) / qdiff));
                        b = 0;
                    }
                    else
                    {
                        r = 255;
                        g = 0;
                        b = 0;
                    }
                }
                else
                {
                    if ( st >= minVal )
                    {
                        r = 0;
                        g = 0;
                        b = 255;
                    }
                    else if ( st > lowQVal )
                    {
                        r = 0;
                        g = (byte) (255 * (st - minVal) / qdiff);
                        b = 255;
                    }
                    else if ( st > midVal )
                    {
                        r = 0;
                        g = 255;
                        b = (byte) (255 * (1 - (st - lowQVal) / qdiff));
                    }
                    else if ( st > uppQVal )
                    {
                        r = (byte) (255 * (st - midVal) / qdiff);
                        g = 255;
                        b = 0;
                    }
                    else if ( st > maxVal )
                    {
                        r = 255;
                        g = (byte) (255 * (1 - (st - uppQVal) / qdiff));
                        b = 0;
                    }
                    else
                    {
                        r = 255;
                        g = 0;
                        b = 0;
                    }
                }
                Color endColour = Color.FromRgb( r , g , b );
                Point endPoint = new Point( x_max / (scale * factor) * dpiX + originX ,
                    y = yHeight - (y_xmax / (scale * factor) * dpiY + originY) );

                newPolygon.Fill = new LinearGradientBrush( startColour , endColour , startPoint , endPoint );

                newElement.Boundary = newPolygon;

                stressTriMesh.Add( newElement );
            }

            // refresh plotting axes
            BuildAxes();
        }


        /// <summary>
        /// Plots colour scaled stress in each element
        /// </summary>
        public void PlotUnSmoothedStress ()
        {
            // clear existing plots
            ClearPlots();

            // get canvas dimensions/properties
            double originX = OriginOffsetX ,
                   originY = OriginOffsetY ,
                   scale = Scale ,
                   yHeight = ActualHeight;
            Units units = Units;

            // get units dependent scaling factor
            double factor;
            switch ( units )
            {
                case Units.Metres: factor = 0.0254; break;
                case Units.Millimetres: factor = 25.4; break;
                case Units.Feet: factor = 1.0 / 12.0; break;
                default: factor = 1.0; break;
            }

            // set plot type
            MenuItem unSmoothedMenu = (MenuItem) ((MenuItem) ((Menu) ((DockPanel) ((Grid) ((Grid) this.Parent).Parent).Children[0]).Children[0]).Items[1]).Items[4];
            int plotType = 0;
            for ( int i = 0 ; i < unSmoothedMenu.Items.Count ; i++ )
            {
                if ( ((MenuItem) unSmoothedMenu.Items[i]).IsChecked )
                {
                    plotType = i;
                    break;
                }
            }

            // load appropriate stress values
            List<double> stress;
            double minVal , maxVal;
            switch ( plotType )
            {
                case 0: // XX
                    stress = sxxE;
                    maxVal = minSxxE;
                    minVal = maxSxxE;
                    break;
                case 1: // YY
                    stress = syyE;
                    maxVal = minSyyE;
                    minVal = maxSyyE;
                    break;
                case 2: // XY
                    stress = sxyE;
                    maxVal = minSxyE;
                    minVal = maxSxyE;
                    break;
                case 3: // ZZ
                    stress = szzE;
                    maxVal = minSzzE;
                    minVal = maxSzzE;
                    break;
                default: // FBAR
                    stress = fbarE;
                    minVal = minFbarE;
                    maxVal = maxFbarE;
                    break;
            }

            // compute key values for colour scale
            double diff = maxVal - minVal;
            double qdiff = 0.25 * diff;
            double lowQVal = minVal + 0.25 * diff;
            double midVal = minVal + 0.5 * diff;
            double uppQVal = minVal + 0.75 * diff;


            // set mesh to regular coords and
            // create fill colours based on stress
            // at integration points
            Polygon newPolygon;
            double x , y;
            fe3NodedTriElement newElement;
            byte r , g , b;
            double st;
            for ( int i = 0 ; i < triElements.Count ; i++ )
            {
                if ( triElements[i].Material == null ) continue;    // skip inactive elements

                newElement = new fe3NodedTriElement( triElements[i].Number ,
                    nodes[triElements[i].Nodes[0].Number] ,
                    nodes[triElements[i].Nodes[1].Number] ,
                    nodes[triElements[i].Nodes[2].Number] ,
                    false , false ,
                    null , false );

                newPolygon = new Polygon();
                newPolygon.StrokeThickness = 0;
                newPolygon.Stroke = Brushes.Transparent;
                newPolygon.Opacity = 1.0;

                // compute fill colour
                st = stress[i];
                if ( plotType == 4 )
                {
                    if ( st <= minVal )
                    {
                        r = 0;
                        g = 0;
                        b = 255;
                    }
                    else if ( st < lowQVal )
                    {
                        r = 0;
                        g = (byte) (255 * (st - minVal) / qdiff);
                        b = 255;
                    }
                    else if ( st < midVal )
                    {
                        r = 0;
                        g = 255;
                        b = (byte) (255 * (1 - (st - lowQVal) / qdiff));
                    }
                    else if ( st < uppQVal )
                    {
                        r = (byte) (255 * (st - midVal) / qdiff);
                        g = 255;
                        b = 0;
                    }
                    else if ( st < maxVal )
                    {
                        r = 255;
                        g = (byte) (255 * (1 - (st - uppQVal) / qdiff));
                        b = 0;
                    }
                    else
                    {
                        r = 255;
                        g = 0;
                        b = 0;
                    }
                }
                else
                {
                    if ( st >= minVal )
                    {
                        r = 0;
                        g = 0;
                        b = 255;
                    }
                    else if ( st > lowQVal )
                    {
                        r = 0;
                        g = (byte) (255 * (st - minVal) / qdiff);
                        b = 255;
                    }
                    else if ( st > midVal )
                    {
                        r = 0;
                        g = 255;
                        b = (byte) (255 * (1 - (st - lowQVal) / qdiff));
                    }
                    else if ( st > uppQVal )
                    {
                        r = (byte) (255 * (st - midVal) / qdiff);
                        g = 255;
                        b = 0;
                    }
                    else if ( st > maxVal )
                    {
                        r = 255;
                        g = (byte) (255 * (1 - (st - uppQVal) / qdiff));
                        b = 0;
                    }
                    else
                    {
                        r = 255;
                        g = 0;
                        b = 0;
                    }
                }
                newPolygon.Fill = new SolidColorBrush( Color.FromRgb( r , g , b ) );

                foreach ( feNode node in newElement.Nodes )
                {
                    x = node.X / (scale * factor) * dpiX + originX;
                    y = yHeight - (node.Y / (scale * factor) * dpiY + originY);
                    newPolygon.Points.Add( new Point( x , y ) );
                }

                newElement.Boundary = newPolygon;

                stressTriMesh.Add( newElement );
            }

            // refresh plotting axes
            BuildAxes();
        }


        // ----------------------------------
        // OVERRIDES
        // ----------------------------------

        protected override void OnMouseLeftButtonUp ( MouseButtonEventArgs e )
        {
            base.OnMouseLeftButtonUp( e );

            Point p = Mouse.GetPosition( this );

            switch ( DrawMode )
            {
                case (DrawModes.Pan):
                    {
                        // End panning operations
                        panning = false;
                        this.Cursor = ((TextBlock) (((MainWindow) ((Grid) ((TabControl) ((TabItem) ((Grid) source.Parent).Parent).Parent).Parent).Parent).Resources["handCursor"])).Cursor;
                    }
                    break;


                case (DrawModes.ZoomArea):
                    {
                        // End zooming operations
                        zooming = false;

                        // Zoom to highlighted region
                        if ( zoomRect.Boundary.Points.Count == 4 )
                        {
                            ZoomArea( zoomRect.Boundary );
                        }

                        // Clean up resources
                        zoomRect.Boundary.Points.Clear();
                        Mouse.Capture( this , CaptureMode.None );
                    }
                    break;


                case (DrawModes.Select):
                    {
                        
                    }
                    break;
            }
        }


        protected override void OnMouseMove ( MouseEventArgs e )
        {
            base.OnMouseMove( e );

            Point p = Mouse.GetPosition( this );

            /*
             * Pan Mode
             * (NB: This is also activated during any mode with the middle mouse button)
             */
            if ( (DrawMode == DrawModes.Pan && Mouse.LeftButton == MouseButtonState.Pressed)
                || Mouse.MiddleButton == MouseButtonState.Pressed )
            {
                // Initialize panning operations
                if ( !panning )
                {
                    panning = true;
                    this.Cursor = ((TextBlock) (((MainWindow) ((Grid) ((TabControl) ((TabItem) ((Grid) source.Parent).Parent).Parent).Parent).Parent).Resources["grabCursor"])).Cursor;

                    // Set reference point for panning translation
                    transPoint = p;
                }
                else
                {
                    // Translate canvas and axes
                    Translate( p - transPoint );

                    // Update reference point
                    transPoint = p;
                }
            }


            /*
             * Zoom Area Mode
             */
            if ( this.DrawMode == DrawModes.ZoomArea && Mouse.LeftButton == MouseButtonState.Pressed )
            {
                // Initialize zooming operation
                if ( !zooming )
                {
                    zooming = true;
                    Mouse.Capture( this , CaptureMode.SubTree );

                    // Add corner points to zoom rectangle
                    // (NB: Polygon was opted for over Rectangle since
                    //      coordinate updates require fewer operations)
                    zoomRect.Boundary.Points.Add( p );
                    zoomRect.Boundary.Points.Add( p );
                    zoomRect.Boundary.Points.Add( p );
                    zoomRect.Boundary.Points.Add( p );
                }
                else
                {
                    Point zoomRectPt;   // For unboxing and updating zoom rectangle coords

                    // Point 0 remains at (Original X, Original Y)

                    // Point 1 moves to (Current X, Original Y)
                    zoomRectPt = zoomRect.Boundary.Points[1];
                    zoomRectPt.X = p.X;
                    zoomRectPt.Y = zoomRect.Boundary.Points[0].Y;
                    zoomRect.Boundary.Points[1] = zoomRectPt;

                    // Point 2 moves to (Current X, Current Y)
                    zoomRect.Boundary.Points[2] = p;

                    // Point 3 moves to (Original X, Current Y)
                    zoomRectPt = zoomRect.Boundary.Points[3];
                    zoomRectPt.X = zoomRect.Boundary.Points[0].X;
                    zoomRectPt.Y = p.Y;
                    zoomRect.Boundary.Points[3] = zoomRectPt;
                }
            }
        }


        protected override void OnMouseUp ( MouseButtonEventArgs e )
        {
            base.OnMouseUp( e );

            /*
             * Pan Mode (drawing mode independent middle mouse button version)
             */
            if ( e.ChangedButton == MouseButton.Middle )
            {
                // End panning operations
                panning = false;

                switch ( DrawMode )
                {
                    case DrawModes.Pan:
                        this.Cursor = ((TextBlock) (((MainWindow) ((Grid) ((TabControl) ((TabItem) ((Grid) source.Parent).Parent).Parent).Parent).Parent).Resources["handCursor"])).Cursor;
                        break;
                    case DrawModes.ZoomArea:
                        this.Cursor = ((TextBlock) (((MainWindow) ((Grid) ((TabControl) ((TabItem) ((Grid) source.Parent).Parent).Parent).Parent).Parent).Resources["zoomAreaCursor"])).Cursor;
                        break;
                    default:
                        this.Cursor = Cursors.Arrow;
                        break;
                }
            }
        }


        protected override void OnMouseRightButtonUp ( MouseButtonEventArgs e )
        {
            base.OnMouseRightButtonUp( e );

            // End drawing
            CancelDrawing();

            // Reset cursor and drawing mode
            this.Cursor = Cursors.Arrow;
            DrawMode = DrawModes.Select;
        }


        protected override void OnMouseWheel ( MouseWheelEventArgs e )
        {
            base.OnMouseWheel( e );

            // Update mouse scroll sentinel
            mouseDelta += e.Delta;

            // Zoom in (centred on mouse position)
            while ( mouseDelta >= 120 )
            {
                Zoom( 1.1 , e.GetPosition( this ) );
                mouseDelta -= 120;
            }

            // Zoom out (centred on mouse position)
            while ( mouseDelta <= -120 )
            {
                Zoom( 1 / 1.1 , e.GetPosition( this ) );
                mouseDelta += 120;
            }
        }


        // ----------------------------------
        // CANVAS EVENTS
        // ----------------------------------


        private void SlopePlotCanvas_SizeChanged ( object sender , SizeChangedEventArgs e )
        {
            // This only occurs once, immediately after canvas intialization
            if ( !IsScaled )
            {
                // Get units dependent scaling factor
                double factor;
                switch ( Units )
                {
                    case Units.Metres: factor = 0.0254; break;
                    case Units.Millimetres: factor = 25.4; break;
                    case Units.Feet: factor = 1.0 / 12.0; break;
                    default: factor = 1.0; break;
                }

                // Compute required initial scale
                double canvasWidth = (ActualWidth - 50) / dpiX * factor;
                double canvasHeight = (ActualHeight - 50) / dpiY * factor;
                Scale = Math.Max( (XAxisMax - XAxisMin) / canvasWidth , (YAxisMax - YAxisMin) / canvasHeight );

                // Get axis references
                xAxis = (Grid) ((Grid) this.Parent).Children[0];
                yAxis = (Grid) ((Grid) this.Parent).Children[1];
                infoBlock = (Grid) ((Grid) this.Parent).Children[2];

                // load substruct information for plotting
                LoadSubstructData();

                // Set plot mode and construct axes and grid
                PlotMode = PlotModes.DeformedMesh;

                // Centre and fit view
                CentreAndFitExtents( true );

                IsScaled = true;
            }
            else
            {
                // Compute translation factor for y coordinates
                double deltaY = e.NewSize.Height - e.PreviousSize.Height;

                Line line;      // For unboxing axis lines

                // Update coordinates of y axis lines
                for ( int i = 2 ; i < yAxis.Children.Count ; i++ )
                {
                    if ( yAxis.Children[i] is Line )
                    {
                        line = yAxis.Children[i] as Line;
                        line.Y1 = line.Y2 += deltaY;
                    }
                }

                Vector delta = new Vector( 0 , deltaY );

                // Update substructs
                substructs.ForEach( delegate( MaterialBlock mb ) { mb.Translate( delta ); } );

                // Update FEA elements
                deformedTriMesh.ForEach( delegate( fe3NodedTriElement element ) { element.Translate( delta ); } );

                // Update disp vectors
                dispVectors.ForEach( delegate( DisplacementVector dv ) { dv.Translate( delta ); } );

                // Update plastic points
                plasticPoints.ForEach( delegate( PlasticPoint pp ) { pp.Translate( delta ); } );

                // Update stress plot
                stressTriMesh.ForEach( delegate( fe3NodedTriElement element ) { element.Translate( delta ); } );
            }
        }
    }
}
