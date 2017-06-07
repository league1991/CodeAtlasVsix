using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            ResourceSetter resMgr = new ResourceSetter(this);
            resMgr.SetStyle();

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
            AddCommand(OnDelectSelectedItems, Key.Delete, ModifierKeys.Alt);
            AddCommand(OnDeleteSelectedItemsAndAddToStop, Key.I);
            AddCommand(OnAddSimilarCodeItem, Key.S);
            AddCommand(OnToggleScheme1, Key.D1, ModifierKeys.Control);
            AddCommand(OnToggleScheme2, Key.D2, ModifierKeys.Control);
            AddCommand(OnToggleScheme3, Key.D3, ModifierKeys.Control);
            AddCommand(OnToggleScheme4, Key.D4, ModifierKeys.Control);
            AddCommand(OnToggleScheme5, Key.D5, ModifierKeys.Control);
            AddCommand(OnShowScheme1, Key.D1, ModifierKeys.Alt);
            AddCommand(OnShowScheme2, Key.D2, ModifierKeys.Alt);
            AddCommand(OnShowScheme3, Key.D3, ModifierKeys.Alt);
            AddCommand(OnShowScheme4, Key.D4, ModifierKeys.Alt);
            AddCommand(OnShowScheme5, Key.D5, ModifierKeys.Alt);
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
                UpdateUI();
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
            UpdateUI();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DBManager.Instance().CloseDB();
            UpdateUI();
        }

        public void UpdateUI()
        {
            schemeWindow.UpdateScheme();
            symbolWindow.UpdateForbiddenSymbol();
            symbolWindow.UpdateSymbol("", "");
            searchWindow.OnSearch();
        }

        public void OnShowInAtlas(object sender, ExecutedRoutedEventArgs e)
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            Document doc = dte.ActiveDocument;
            EnvDTE.TextSelection ts = doc.Selection as EnvDTE.TextSelection;
            int lineOffset = ts.AnchorPoint.LineCharOffset;
            int lineNum = ts.AnchorPoint.Line;

            ts.SelectLine();
            string lineText = ts.Text;
            ts.MoveToLineAndOffset(lineNum, lineOffset);

            Regex rx = new Regex(@"\b(?<word>\w+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection matches = rx.Matches(lineText);

            // Report on each match.
            string token = null;
            foreach (Match match in matches)
            {
                string word = match.Groups["word"].Value;
                int startIndex = match.Index;
                int endIndex = startIndex + word.Length - 1;
                int lineIndex = lineOffset - 1;
                if (startIndex <= lineIndex && endIndex + 1 >= lineIndex)
                {
                    token = word;
                    break;
                }
            }

            if (token != null)
            {
                string docPath = doc.FullName;
                searchWindow.nameEdit.Text = token;
                searchWindow.typeEdit.Text = "";
                searchWindow.fileEdit.Text = docPath;
                searchWindow.lineEdit.Text = lineNum.ToString();
                searchWindow.OnSearch();
                searchWindow.OnAddToScene();
            }
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
        #endregion

        #region Delete
        public void OnDelectSelectedItems(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.DeleteSelectedItems(false);
        }
        public void OnDeleteSelectedItemsAndAddToStop(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            if (scene != null)
            {
                scene.DeleteSelectedItems(true);
                var mainUI = UIManager.Instance().GetMainUI();
                mainUI.symbolWindow.UpdateForbiddenSymbol();
            }
        }
        #endregion

        #region Add Symbol
        public void OnAddSimilarCodeItem(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.AddSimilarCodeItem();
        }
        #endregion

        #region Scheme
        void ToggleSelectedEdgeToScheme(int ithScheme)
        {
            var scene = UIManager.Instance().GetScene();
            scene.ToggleSelectedEdgeToScheme(ithScheme);
        }

        public void OnToggleScheme1(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleSelectedEdgeToScheme(0);
        }

        public void OnToggleScheme2(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleSelectedEdgeToScheme(1);
        }

        public void OnToggleScheme3(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleSelectedEdgeToScheme(2);
        }

        public void OnToggleScheme4(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleSelectedEdgeToScheme(3);
        }

        public void OnToggleScheme5(object sender, ExecutedRoutedEventArgs e)
        {
            ToggleSelectedEdgeToScheme(4);
        }

        public void ShowScheme(int ithScheme, bool isSelected = false)
        {
            var scene = UIManager.Instance().GetScene();
            scene.ShowIthScheme(ithScheme, isSelected);
        }

        public void OnShowScheme1(object sender, ExecutedRoutedEventArgs e)
        {
            ShowScheme(0);
        }

        public void OnShowScheme2(object sender, ExecutedRoutedEventArgs e)
        {
            ShowScheme(1);
        }

        public void OnShowScheme3(object sender, ExecutedRoutedEventArgs e)
        {
            ShowScheme(2);
        }

        public void OnShowScheme4(object sender, ExecutedRoutedEventArgs e)
        {
            ShowScheme(3);
        }

        public void OnShowScheme5(object sender, ExecutedRoutedEventArgs e)
        {
            ShowScheme(4);
        }
        #endregion
        public SymbolWindow GetSymbolWindow()
        {
            return this.symbolWindow;
        }
    }
}
