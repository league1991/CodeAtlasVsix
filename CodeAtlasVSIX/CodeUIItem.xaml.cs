using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Interaction logic for CodeUIItem.xaml.
    /// </summary>
    [ProvideToolboxControl("CodeAtlasVSIX.CodeUIItem", true)]
    public partial class CodeUIItem : UserControl
    {
        public CodeUIItem()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentUICulture, "We are inside {0}.Button1_Click()", this.ToString()));
        }

        private void circle_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {

        }

        private void circle_DragOver(object sender, DragEventArgs e)
        {

        }

        private void circle_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MessageBox.Show(string.Format(CultureInfo.CurrentUICulture, "We are inside {0}.Button1_Click()", this.ToString()));
        }
    }
}
