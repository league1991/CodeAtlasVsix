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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// MainUI.xaml 的交互逻辑
    /// </summary>
    public partial class MainUI : DockPanel
    {
        public MainUI()
        {
            InitializeComponent();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            //ofd.DefaultExt = ".xml";
            //ofd.Filter = "xml file|*.xml";
            if (ofd.ShowDialog() == true)
            {
                DBManager.Instance().OpenDB(ofd.FileName);
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // defaultPath = r'C:\Users\me\AppData\Roaming\Sublime Text 3\Packages\CodeAtlas\CodeAtlasSublime.udb'
            // defaultPath = 'I:/Programs/masteringOpenCV/Chapter3_MarkerlessAR/doc/xml/index.xml'
            // defaultPath = 'I:/Programs/mitsuba/doxygenData/xml/index.xml'
            var defaultPath = "D:/Code/NewRapidRT/rapidrt/Doxyfile";
            DBManager.Instance().OpenDB(defaultPath);
        }
    }
}
