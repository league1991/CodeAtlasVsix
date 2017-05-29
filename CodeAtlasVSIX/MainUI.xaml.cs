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
        List<KeyBinding> m_keyCommands = new List<KeyBinding>();

        public MainUI()
        {
            InitializeComponent();

            AddCommand(OnFindCallers, Key.C);
            AddCommand(OnFindCallees, Key.V);
            AddCommand(OnFindMembers, Key.M);
            AddCommand(OnFindOverrides, Key.O);
            AddCommand(OnFindBases, Key.B);
            AddCommand(OnFindUses, Key.U);
            AddCommand(OnGoUp, Key.Up);
            AddCommand(OnGoDown, Key.Down);
            AddCommand(OnGoLeft, Key.Left);
            AddCommand(OnGoRight, Key.Right);
            AddCommand(OnDelectSelectedItems, Key.Delete, ModifierKeys.None);
        }

        public void AddCommand(ExecutedRoutedEventHandler callback, Key key, ModifierKeys modifier = ModifierKeys.Alt)
        {
            CommandBinding cmd = new CommandBinding();
            cmd.Command = new RoutedUICommand();

            cmd.Executed += callback;
            cmd.CanExecute += new CanExecuteRoutedEventHandler(_AlwaysCanExecute);
            this.CommandBindings.Add(cmd);
            KeyBinding CmdKey = new KeyBinding();
            CmdKey.Key = key;
            CmdKey.Modifiers = modifier;
            CmdKey.Command = cmd.Command;
            this.InputBindings.Add(CmdKey);
            m_keyCommands.Add(CmdKey);
        }

        private void _AlwaysCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
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
            var defaultPath = "I:/Programs/mitsuba/Doxyfile";
            //var defaultPath = "D:/Code/NewRapidRT/rapidrt/Doxyfile";

            var newDownTime = DateTime.Now;
            DBManager.Instance().OpenDB(defaultPath);
            double duration = (DateTime.Now - newDownTime).TotalSeconds;
            Console.WriteLine("open time:" + duration.ToString());
        }

        #region Find References
        void _FindRefs(string refStr, string entStr, bool inverseEdge = false, int maxCount = -1)
        {
            System.Console.WriteLine("FindRef: " + refStr + " " + entStr);
            var scene = UIManager.Instance().GetScene();
            scene.AddRefs(refStr, entStr, inverseEdge, maxCount);
        }

        public void OnFindCallers(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("callby", "function, method");
        }
        public void OnFindCallees(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("call", "function, method", true);
        }
        public void OnFindMembers(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("declare,define", "variable, object", true, 1);
            _FindRefs("declare,define", "function", true, 1);
            _FindRefs("declarein,definein", "function,class", false);
        }
        public void OnFindOverrides(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("overrides", "function, method", false);
            _FindRefs("overriddenby", "function, method", true);
        }
        public void OnFindBases(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("base", "class,struct", false);
            _FindRefs("derive", "class,struct", true);
        }
        public void OnFindUses(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("useby", "function, method, class, struct", false);
            _FindRefs("use", "variable,object", true);
        }
        #endregion

        #region Navigation
        public void OnGoUp(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(0.0, -1.0));
        }
        public void OnGoDown(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(0.0, 1.0));
        }
        public void OnGoLeft(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(-1.0, 0.0));
        }
        public void OnGoRight(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(1.0, 0.0));
        }
        public void OnDelectSelectedItems(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.DeleteSelectedItems(false);
        }
        #endregion

        public SymbolWindow GetSymbolWindow()
        {
            return this.symbolWindow;
        }
    }
}
