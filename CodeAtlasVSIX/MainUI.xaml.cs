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
            // defaultPath = r"C:\Users\me\AppData\Roaming\Sublime Text 3\Packages\CodeAtlas\CodeAtlasSublime.udb"
            // defaultPath = "I:/Programs/masteringOpenCV/Chapter3_MarkerlessAR/doc/xml/index.xml"
            // var defaultPath = "I:/Programs/mitsuba/Doxyfile";
            var defaultPath = "D:/Code/NewRapidRT/rapidrt/Doxyfile";

            var newDownTime = DateTime.Now;
            DBManager.Instance().OpenDB(defaultPath);
            double duration = (DateTime.Now - newDownTime).TotalSeconds;
            Console.WriteLine("open time:" + duration.ToString());
        }

        #region Find References
        void _FindRefs(string refStr, string entStr, bool inverseEdge = false, int maxCount = -1)
        {
            var scene = UIManager.Instance().GetScene();
            scene.AddRefs(refStr, entStr, inverseEdge, maxCount);
        }

        public void OnFindCallers()
        {
            _FindRefs("callby", "function, method");
        }
        public void OnFindCallees()
        {
            _FindRefs("call", "function, method", true);
        }
        public void OnFindMembers()
        {
            _FindRefs("declare,define", "variable, object", true, 1);
            _FindRefs("declare,define", "function", true, 1);
            _FindRefs("declarein,definein", "function,class", false);
        }

        public void OnFindOverrides()
        {
            _FindRefs("overrides", "function, method", false);
            _FindRefs("overriddenby", "function, method", true);
        }

        public void OnFindBases()
        {
            _FindRefs("base", "class,struct", false);
            _FindRefs("derive", "class,struct", true);
        }

        public void OnFindUses()
        {
            _FindRefs("useby", "function, method, class, struct", false);
            _FindRefs("use", "variable,object", true);
        }

        #endregion

        private void DockPanel_KeyDown(object sender, KeyEventArgs e)
        {
            System.Console.WriteLine("keyDown" + e.ToString());
        }

        private void CodeView_KeyDown(object sender, KeyEventArgs e)
        {
            System.Console.WriteLine("keyDown view" + e.ToString());
        }

        private void DockPanel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Console.WriteLine("preview keyDown view" + e.ToString());
        }
    }
}
