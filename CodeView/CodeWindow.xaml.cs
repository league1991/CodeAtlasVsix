using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CodeView
{
    /// <summary>
    /// CodeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CodeWindow : Window
    {
        public CodeWindow()
        {
            InitializeComponent();

            //this.AddChild(new CodeAtlasVSIX.CodeView());
            this.Content = new CodeAtlasVSIX.CodeView();
        }
    }
}
