//------------------------------------------------------------------------------
// <copyright file="CodeAtlasControl.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace CodeAtlasVSIX
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    /// <summary>
    /// Interaction logic for CodeAtlasControl.
    /// </summary>
    public partial class CodeAtlasControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeAtlasControl"/> class.
        /// </summary>
        public CodeAtlasControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
                "CodeAtlas");
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            this.canvas.Children.Add(new CodeUIItem());
        }

        private void onMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            Point position = e.GetPosition(this.canvas);
            scaleValue += e.Delta * 0.005;
            ScaleTransform scale = new ScaleTransform(scaleValue, scaleValue, position.X, position.Y);
            this.canvas.LayoutTransform = scale;
            this.canvas.UpdateLayout();
        }

        private double scaleValue = 1.0;
    }
}