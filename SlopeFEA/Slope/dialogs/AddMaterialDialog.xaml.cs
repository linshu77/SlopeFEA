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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SlopeFEA
{
    /// <summary>
    /// Interaction logic for AddMaterialDialog.xaml
    /// </summary>
    public partial class AddMaterialDialog : Window
    {
        private SlopeCanvas canvas;

        public AddMaterialDialog ( Window owner )
        {
            InitializeComponent();

            this.Owner = owner;

            canvas = (SlopeCanvas) ((Grid) ((TabControl) ((Grid) this.Owner.Content).Children[2]).SelectedContent).Children[2];

            canvas.MaterialTypes.ForEach( delegate( MaterialType mt ) { materialList.Items.Add( mt ); } );

            switch ( canvas.Units )
            {
                case Units.Metres:
                    cohUnits.Content = emodUnits.Content = "kPa";
                    gammaUnits.Content = "kN/m^3";
                    break;
                case Units.Millimetres:
                    cohUnits.Content = emodUnits.Content = "kPa";
                    gammaUnits.Content = "kN/m^3";
                    break;
                case Units.Feet:
                    cohUnits.Content = emodUnits.Content = "psi";
                    gammaUnits.Content = "pcf";
                    break;
                default:
                    cohUnits.Content = emodUnits.Content = "psi";
                    gammaUnits.Content = "pcf";
                    break;
            }

            colour.Fill = Brushes.Transparent;
        }

        private void materialList_SelectionChanged ( object sender , SelectionChangedEventArgs e )
        {
            if ( materialList.SelectedItem is MaterialType )
            {
                add.IsEnabled = false;
                modify.IsEnabled = true;
                remove.IsEnabled = true;

                MaterialType currMaterial = materialList.SelectedItem as MaterialType;

                colour.Fill = currMaterial.Fill;
                phi.Text = string.Format( "{0}" , Math.Round( currMaterial.Phi , 2 ) );
                coh.Text = string.Format( "{0}" , Math.Round( currMaterial.Cohesion , 2 ) );
                psi.Text = string.Format( "{0}" , Math.Round( currMaterial.Psi , 2 ) );
                gamma.Text = string.Format( "{0}" , Math.Round( currMaterial.Gamma , 2 ) );
                emod.Text = string.Format( "{0}" , Math.Round( currMaterial.Emod , 2 ) );
                nu.Text = string.Format( "{0}" , Math.Round( currMaterial.Nu , 2 ) );
            }
            else
            {
                if ( this.IsInitialized )
                {
                    add.IsEnabled = true;
                    modify.IsEnabled = false;
                    remove.IsEnabled = false;

                    colour.Fill = Brushes.Transparent;
                    phi.Text = "";
                    coh.Text = "";
                    psi.Text = "";
                    gamma.Text = "";
                    emod.Text = "";
                    nu.Text = "";
                }
            }
        }

        private void chooseColour_Click ( object sender , RoutedEventArgs e )
        {
            System.Windows.Forms.ColorDialog colourDlg = new System.Windows.Forms.ColorDialog();
            colourDlg.AllowFullOpen = true;
            colourDlg.FullOpen = true;
            colourDlg.ShowDialog();

            Color newColour = new Color();
            newColour.A = colourDlg.Color.A;
            newColour.R = colourDlg.Color.R;
            newColour.G = colourDlg.Color.G;
            newColour.B = colourDlg.Color.B;

            colour.Fill = new SolidColorBrush( newColour );
        }

        private void add_Click ( object sender , RoutedEventArgs e )
        {
            MaterialType newMaterial = new MaterialType();

            if ( materialList.Text == "Add new material..." || materialList.Text == "" )
            {
                MessageBox.Show( "Must give the material a name." , "Error" );
                return;
            }

            if ( materialList.Text == "NULL" )
            {
                MessageBox.Show( "NULL material name is reserved." , "Error" );
                return;
            }

            if ( colour.Fill == Brushes.Transparent )
            {
                MessageBox.Show( "Must define a colour." , "Error" );
                return;
            }

            double newPhi;
            if ( !double.TryParse( phi.Text , out newPhi ) || newPhi < 0 )
            {
                MessageBox.Show( "Angle of friction must be a positive value." , "Error" );
                return;
            }

            double newCoh;
            if ( !double.TryParse( coh.Text , out newCoh ) || newCoh < 0 )
            {
                MessageBox.Show( "Cohesion must be a positive value." , "Error" );
                return;
            }

            double newPsi;
            if ( !double.TryParse( psi.Text , out newPsi ) || newPsi < 0 )
            {
                MessageBox.Show( "Dilatancy angle must be a positive value." , "Error" );
                return;
            }

            double newGamma;
            if ( !double.TryParse( gamma.Text , out newGamma ) || newGamma < 0 )
            {
                MessageBox.Show( "Unit weight must be a positive value." , "Error" );
                return;
            }

            double newEmod;
            if ( !double.TryParse( emod.Text , out newEmod ) || newEmod < 0 )
            {
                MessageBox.Show( "Elastic modulus must be a positive value." , "Error" );
                return;
            }

            double newNu;
            if ( !double.TryParse( nu.Text , out newNu ) || newNu < 0 || newNu >= 0.5 )
            {
                MessageBox.Show( "Poisson's ratio must be a value in the range: 0 <= nu < 0.5" , "Error" );
                return;
            }

            newMaterial.Name = materialList.Text;
            newMaterial.Fill = colour.Fill;
            newMaterial.Phi = newPhi;
            newMaterial.Cohesion = newCoh;
            newMaterial.Psi = newPsi;
            newMaterial.Gamma = newGamma;
            newMaterial.Emod = newEmod;
            newMaterial.Nu = newNu;

            canvas.MaterialTypes.Insert( canvas.MaterialTypes.Count - 1 , newMaterial );

            materialList.Items.Clear();

            materialList.Items.Add( "Add new material..." );

            for ( int i = 0 ; i < canvas.MaterialTypes.Count - 1 ; i++ )
            {
                materialList.Items.Add( canvas.MaterialTypes[i] );
            }

            materialList.SelectedIndex = 0;
            materialList.Focus();

            canvas.IsSaved = false;
        }

        private void modify_Click ( object sender , RoutedEventArgs e )
        {

            MaterialType currMaterial = materialList.SelectedItem as MaterialType;

            if ( colour.Fill == Brushes.Transparent )
            {
                MessageBox.Show( "Must define a colour." , "Error" );
                return;
            }

            double newPhi;
            if ( !double.TryParse( phi.Text , out newPhi ) || newPhi < 0 )
            {
                MessageBox.Show( "Angle of friction must be a positive value." , "Error" );
                return;
            }

            double newCoh;
            if ( !double.TryParse( coh.Text , out newCoh ) || newCoh < 0 )
            {
                MessageBox.Show( "Cohesion must be a positive value." , "Error" );
                return;
            }

            double newPsi;
            if ( !double.TryParse( psi.Text , out newPsi ) || newPsi < 0 )
            {
                MessageBox.Show( "Dilatancy angle must be a positive value." , "Error" );
                return;
            }

            double newGamma;
            if ( !double.TryParse( gamma.Text , out newGamma ) || newGamma < 0 )
            {
                MessageBox.Show( "Unit weight must be a positive value." , "Error" );
                return;
            }

            double newEmod;
            if ( !double.TryParse( emod.Text , out newEmod ) || newEmod < 0 )
            {
                MessageBox.Show( "Elastic modulus must be a positive value." , "Error" );
                return;
            }

            double newNu;
            if ( !double.TryParse( nu.Text , out newNu ) || newNu < 0 || newNu >= 0.5 )
            {
                MessageBox.Show( "Poisson's ratio must be a number in the range: 0 <= nu < 0.5" , "Error" );
                return;
            }

            currMaterial.Fill = colour.Fill;
            currMaterial.Phi = newPhi;
            currMaterial.Cohesion = newCoh;
            currMaterial.Psi = newPsi;
            currMaterial.Gamma = newGamma;
            currMaterial.Emod = newEmod;
            currMaterial.Nu = newNu;

            materialList.SelectedIndex = 0;
            materialList.Focus();

            canvas.IsSaved = false;
        }

        private void remove_Click ( object sender , RoutedEventArgs e )
        {
            MaterialType currMaterial = materialList.SelectedItem as MaterialType;

            if ( currMaterial != null )
            {
                canvas.MaterialTypes.Remove( currMaterial );

                for ( int i = 0 ; i < canvas.MaterialBlocks.Count ; i++ )
                {
                    if ( canvas.MaterialBlocks[i].Material == currMaterial )
                    {
                        canvas.MaterialBlocks[i].Material = canvas.MaterialTypes[canvas.MaterialTypes.Count - 1];
                    }
                }

                materialList.Items.Clear();

                materialList.Items.Add( "Add new material..." );

                for ( int i = 0 ; i < canvas.MaterialTypes.Count - 1 ; i++ )
                {
                    materialList.Items.Add( canvas.MaterialTypes[i] );
                }

                materialList.SelectedIndex = 0;
                materialList.Focus();

                canvas.IsSaved = false;
                canvas.IsVerified = false;
            }
        }
    }
}
