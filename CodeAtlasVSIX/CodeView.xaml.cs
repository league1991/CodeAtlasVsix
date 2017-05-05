using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Interaction logic for CodeView.xaml.
    /// </summary>
    [ProvideToolboxControl("CodeAtlasVSIX.CodeView", true)]
    public partial class CodeView : UserControl
    {
        public double scaleValue = 1.0;
        public CodeView()
        {
            InitializeComponent();
        }
        
        private void testButton_Click(object sender, RoutedEventArgs e)
        {
            this.canvas.Children.Add(new CodeUIItem());
        }

        private void canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            Point position = e.GetPosition(this.canvas);
            scaleValue += e.Delta * 0.005;
            ScaleTransform scale = new ScaleTransform(scaleValue, scaleValue, position.X, position.Y);
            this.canvas.LayoutTransform = scale;
            this.canvas.UpdateLayout();
        }
    }
}
