using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
        public Package m_package;
        // Switch for all commands
        bool m_isCommandEnable = true;

        ReferenceSearcher m_refSearcher;
        DateTime m_lastCheckRefTime = DateTime.Now;
        int m_checkCount = 0;

        public MainUI()
        {
            InitializeComponent();

            ResourceSetter resMgr = new ResourceSetter(this);
            m_refSearcher = new ReferenceSearcher();
            resMgr.SetStyle();

            analysisWindow.InitLanguageOption();

            AddCommand(OnFindCallers, Key.C, ModifierKeys.Alt);
            AddCommand(OnFindCallees, Key.V, ModifierKeys.Alt);
            AddCommand(OnFindMembers, Key.M, ModifierKeys.Alt);
            AddCommand(OnFindOverrides, Key.O, ModifierKeys.Alt);
            AddCommand(OnFindBases, Key.B, ModifierKeys.Alt);
            AddCommand(OnFindUses, Key.U, ModifierKeys.Alt);
            AddCommand(OnGoUp, Key.I, ModifierKeys.Alt);
            AddCommand(OnGoDown, Key.K, ModifierKeys.Alt);
            AddCommand(OnGoLeft, Key.J, ModifierKeys.Alt);
            AddCommand(OnGoRight, Key.L, ModifierKeys.Alt);
            AddCommand(OnGoUp, Key.Up, ModifierKeys.None);
            AddCommand(OnGoDown, Key.Down, ModifierKeys.None);
            AddCommand(OnGoLeft, Key.Left, ModifierKeys.None);
            AddCommand(OnGoRight, Key.Right, ModifierKeys.None);
            AddCommand(OnDelectSelectedItems, Key.Delete, ModifierKeys.None);
            AddCommand(OnDelectSelectedItems, Key.Delete, ModifierKeys.Alt);
            AddCommand(OnDeleteNearbyItems, Key.D, ModifierKeys.Alt);
            AddCommand(OnDeleteSelectedItemsAndAddToStop, Key.N, ModifierKeys.Alt);
            AddCommand(OnAddSimilarCodeItem, Key.S, ModifierKeys.Alt);
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
            AddCommand(OnBeginCustomEdge, Key.W, ModifierKeys.Alt);
            AddCommand(OnEndCustomEdge, Key.E, ModifierKeys.Alt);

            //RegisterCallback();
        }
        private void RegisterCallback()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            var _findEvents = dte.Events.FindEvents;
            _findEvents.FindDone += new _dispFindEvents_FindDoneEventHandler(OnFindDone);
            CommandEvents  events = dte.Events.get_CommandEvents("{5EFC7975-14BC-11CF-9B2B-00AA00573819}", (int)VSConstants.VSStd97CmdID.FindReferences);
            //CommandEvents events = dte.Events.get_CommandEvents("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}", (int)VSConstants.VSStd97CmdID.FindReferences);
            
            events.AfterExecute += new _dispCommandEvents_AfterExecuteEventHandler(FindRefDone);
        }

        private void OnFindDone(vsFindResult result, bool cancelled)
        {
        }

        private void FindRefDone(string Guid, int ID, object CustomIn, object CustomOut)
        {
        }

        public void SetCommandActive(bool isActive)
        {
            m_isCommandEnable = isActive;
            schemeWindow.Dispatcher.Invoke((ThreadStart)delegate
            {
                this.mask.Visibility = isActive ? Visibility.Hidden : Visibility.Visible;
                this.searchWindow.IsEnabled = isActive;
                this.symbolWindow.IsEnabled = isActive;
                this.schemeWindow.IsEnabled = isActive;
                this.tabControl.IsEnabled = isActive;
                this.menu.IsEnabled = isActive;
            });
        }

        public bool GetCommandActive()
        {
            return m_isCommandEnable;
        }

        public void SetPackage(Package package)
        {
            m_package = package;
            if (m_refSearcher != null)
            {
                m_refSearcher.SetPackage(package);
            }
        }

        public void AddCommand(ExecutedRoutedEventHandler callback, Key key, ModifierKeys modifier)
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

        public void OnOpen(object sender, RoutedEventArgs e)
        {
            if (!GetCommandActive())
            {
                return;
            }
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte != null)
            {
                Solution solution = dte.Solution;
                var solutionFile = solution.FileName;
                if (solutionFile != "")
                {
                    var doxyFolder = System.IO.Path.GetDirectoryName(solutionFile) + "\\CodeGraphData";
                    CheckOrCreateFolder(doxyFolder);
                    ofd.InitialDirectory = doxyFolder;
                }
            }
            ofd.DefaultExt = ".graph";
            ofd.Filter = "Code Graph Analysis Result|*.graph";
            if (ofd.ShowDialog() == true)
            {
                if (DBManager.Instance().GetDB().IsOpen())
                {
                    DBManager.Instance().CloseDB();
                }
                DBManager.Instance().OpenDB(ofd.FileName);
                UpdateUI();
            }
        }

        public void OnOpenDefault(object sender, RoutedEventArgs e)
        {
            if (!GetCommandActive())
            {
                return;
            }

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte != null)
            {
                Solution solution = dte.Solution;
                var solutionFile = solution.FileName;
                if (solutionFile != "")
                {
                    var doxyFolder = System.IO.Path.GetDirectoryName(solutionFile) + "\\CodeGraphData";
                    CheckOrCreateFolder(doxyFolder);
                    var doxyPath = doxyFolder + "\\Result_solution.graph";
                    if (File.Exists(doxyPath))
                    {
                        if (DBManager.Instance().GetDB().IsOpen())
                        {
                            DBManager.Instance().CloseDB();
                        }
                        DBManager.Instance().OpenDB(doxyPath);
                        UpdateUI();
                    }
                    else
                    {
                        Logger.Warning(doxyPath + " doesn't exist. Please analyse solution first.");
                    }
                }
            }
        }

        void CheckOrCreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
        }

        public void OnClose(object sender, RoutedEventArgs e)
        {
            if (!GetCommandActive())
            {
                return;
            }
            DBManager.Instance().CloseDB();
            UpdateUI();
            UIManager.Instance().GetScene().m_selectTimeStamp += 1;
        }

        public void UpdateUI()
        {
            schemeWindow.Dispatcher.Invoke((ThreadStart)delegate
            {
                schemeWindow.UpdateScheme();
            });
            symbolWindow.Dispatcher.Invoke((ThreadStart)delegate
            {
                symbolWindow.UpdateForbiddenSymbol();
                symbolWindow.UpdateSymbol("", "");
            });
            symbolWindow.Dispatcher.Invoke((ThreadStart)delegate
            {
                searchWindow.OnSearch();
            });
            analysisWindow.Dispatcher.Invoke((ThreadStart)delegate
            {
                analysisWindow.UpdateExtensionList();
                analysisWindow.UpdateMacroList();
            });
        }        

        public void OnShowInAtlas(object sender, ExecutedRoutedEventArgs e)
        {
            var result = DoShowInAtlas();

            // Connect edge automatically
            if (result != null && result.bestEntity != null)
            {
                var scene = UIManager.Instance().GetScene();
                var sourceUname = scene.m_customEdgeSource;
                if (sourceUname != null && sourceUname != "")
                {
                    scene.DoAddCustomEdge(sourceUname, result.bestEntity.UniqueName());
                    scene.CancelCustomEdgeMode();
                }
            }
        }

        public DoxygenDB.EntitySearchResult DoShowInAtlas()
        {
            if (!GetCommandActive())
            {
                return null;
            }

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            Document doc = null;
            if (dte != null)
            {
                doc = dte.ActiveDocument;
            }
            if (doc == null)
            {
                return null;
            }
            string token = null;
            string longName = null;
            int lineNum = 0;
            EnvDTE.TextSelection ts = doc.Selection as EnvDTE.TextSelection;
            DoxygenDB.EntitySearchResult result = new DoxygenDB.EntitySearchResult();

            // Create code position            
            bool isWholeLine = ts.AnchorPoint.AtEndOfLine && ts.ActivePoint.AtStartOfLine && ts.AnchorPoint.Line == ts.ActivePoint.Line;
            int activeLine = ts.ActivePoint.Line;
            if (isWholeLine)
            {
                var scene = UIManager.Instance().GetScene();
                string docPath = doc.FullName;
                string fileName = doc.Name;
                int column = 0;
                var uname = scene.GetBookmarkUniqueName(docPath, activeLine, column);
                scene.AddBookmarkItem(docPath, fileName, activeLine, column);
                scene.SelectCodeItem(uname);
                var entity = new DoxygenDB.Entity(uname, docPath, fileName, "page", new Dictionary<string, DoxygenDB.Variant>());
                result.candidateList.Add(entity);
                result.bestEntity = entity;
                return result;
            }
            CursorNavigator.GetCursorWord(ts, out token, out longName, out lineNum);

            var searched = false;
            if (token != null)
            {
                // Search token under cursor
                string docPath = doc.FullName;
                result = SearchAndAddToScene(token, (int)DoxygenDB.SearchOption.MATCH_CASE|(int)DoxygenDB.SearchOption.MATCH_WORD,
                    longName, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.DB_CONTAINS_WORD,
                    "", docPath, lineNum);
                searched = result.candidateList.Count != 0;

                if (!searched)
                {
                    result = SearchAndAddToScene(token, (int)DoxygenDB.SearchOption.MATCH_WORD,
                        longName, (int)DoxygenDB.SearchOption.DB_CONTAINS_WORD,
                        "", docPath, lineNum);
                    searched = result.candidateList.Count != 0;
                }
            }
            else
            {

                // Search parent scope
                Document cursorDoc;
                CodeElement element;
                int line;
                CursorNavigator.GetCursorElement(out cursorDoc, out element, out line);
                if (element != null && cursorDoc != null)
                {
                    var kind = CursorNavigator.VSElementTypeToString(element);
                    result = SearchAndAddToScene(
                        element.Name, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.MATCH_WORD, 
                        element.FullName, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.DB_CONTAINS_WORD,
                        kind, cursorDoc.FullName, lineNum);
                    searched = result.candidateList.Count != 0;
                }
            }
            // Search the whole document
            if (!searched && token == null)
            {
                var fileName = doc.Name;
                var fullPath = doc.FullName.Replace("\\", "/");
                result = SearchAndAddToScene(fileName, (int)DoxygenDB.SearchOption.MATCH_WORD,
                            fileName, (int)DoxygenDB.SearchOption.DB_CONTAINS_WORD,
                            "file", fullPath, 0);
            }
            return result;
        }

        DoxygenDB.EntitySearchResult SearchAndAddToScene(
            string name, int nameOption,
            string longName, int longNameOption,
            string type, string docPath, int lineNum)
        {
            searchWindow.nameEdit.Text = name;
            searchWindow.typeEdit.Text = type;
            searchWindow.fileEdit.Text = docPath;
            searchWindow.lineEdit.Text = lineNum.ToString();

            searchWindow.resultList.Items.Clear();
            var db = DBManager.Instance().GetDB();
            var result = new DoxygenDB.EntitySearchResult();
            if (db == null)
            {
                return result;
            }

            var request = new DoxygenDB.EntitySearchRequest(
                name, nameOption,
                longName, longNameOption,
                type, docPath, lineNum);
            db.SearchAndFilter(request, out result);
            searchWindow.SetSearchResult(result);
            searchWindow.OnAddToScene();
            return result;
        }

        public void OnShowDefinitionInAtlas(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();

            CodeElement srcElement, tarElement;
            Document srcDocument, tarDocument;
            int srcLine, tarLine;
            CursorNavigator.GetCursorElement(out srcDocument, out srcElement, out srcLine);

            Guid cmdGroup = VSConstants.GUID_VSStandardCommandSet97;
            var commandTarget = ((System.IServiceProvider)m_package).GetService(typeof(SUIHostCommandDispatcher)) as IOleCommandTarget;
            if (commandTarget != null)
            {
                int hr = commandTarget.Exec(ref cmdGroup,
                                             (uint)VSConstants.VSStd97CmdID.GotoDefn,
                                             (uint)OLECMDEXECOPT.OLECMDEXECOPT_DODEFAULT,
                                             System.IntPtr.Zero, System.IntPtr.Zero);

            }

            CursorNavigator.GetCursorElement(out tarDocument, out tarElement, out tarLine);
            if (srcElement == null || tarElement == null || srcElement == tarElement)
            {
                return;
            }

            var srcName = srcElement.Name;
            var srcLongName = srcElement.FullName;
            var srcType = CursorNavigator.VSElementTypeToString(srcElement);
            var srcFile = srcDocument.FullName;

            var tarName = tarElement.Name;
            var tarLongName = tarElement.FullName;
            var tarType = CursorNavigator.VSElementTypeToString(tarElement);
            var tarFile = tarDocument.FullName;

            var db = DBManager.Instance().GetDB();
            var srcRequest = new DoxygenDB.EntitySearchRequest(
                srcName, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.MATCH_WORD,
                srcLongName, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.DB_CONTAINS_WORD,
                srcType, srcFile, srcLine);
            var srcResult = new DoxygenDB.EntitySearchResult();
            db.SearchAndFilter(srcRequest, out srcResult);

            //var tarRequest = new DoxygenDB.EntitySearchRequest(
            //    tarName, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.MATCH_WORD,
            //    tarLongName, (int)DoxygenDB.SearchOption.MATCH_CASE | (int)DoxygenDB.SearchOption.DB_CONTAINS_WORD,
            //    tarType, tarFile, tarLine);
            //var tarResult = new DoxygenDB.EntitySearchResult();
            //db.SearchAndFilter(tarRequest, out tarResult);
            var tarResult = DoShowInAtlas();

            DoxygenDB.Entity srcBestEntity, tarBestEntity;
            srcBestEntity = srcResult.bestEntity;
            tarBestEntity = tarResult.bestEntity;
            if (srcBestEntity != null && tarBestEntity != null && srcBestEntity.m_id != tarBestEntity.m_id)
            {
                scene.AcquireLock();
                scene.AddCodeItem(srcBestEntity.m_id);
                scene.AddCodeItem(tarBestEntity.m_id);
                scene.DoAddCustomEdge(srcBestEntity.m_id, tarBestEntity.m_id);
                scene.SelectCodeItem(tarBestEntity.m_id);
                scene.ReleaseLock();
            }
        }

        #region Find References
        void _FindRefs(string refStr, string entStr, bool inverseEdge = false, int maxCount = -1)
        {
            // Logger.WriteLine("FindRef: " + refStr + " " + entStr);
            var scene = UIManager.Instance().GetScene();
            scene.AddRefs(refStr, entStr, inverseEdge, maxCount);
        }

        public void OnFindCallers(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("callby", "function, method");
            _FindRefs("useby", "function, method, class, struct, file", false);
            var scene = UIManager.Instance().GetScene();
            scene.AddProjectDependencies();
        }
        public void OnFindCallees(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("call", "function, method", true);
            _FindRefs("use", "variable,object,file", true, 1);
            var scene = UIManager.Instance().GetScene();
            scene.AddProjectDependencies();
        }
        public void OnFindMembers(object sender, ExecutedRoutedEventArgs e)
        {
            _FindRefs("declare,define,member", "dir,file,namespace", true,1);
            _FindRefs("declare,define,member", "class,struct,typedef", true, 1);
            _FindRefs("declare,define", "variable, object", true, 1);
            _FindRefs("declare,define", "function", true, 1);
            _FindRefs("declarein,definein,memberin", "dir,file,namespace,function,class", false);
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
            var scene = UIManager.Instance().GetScene();
            var selectedItem = scene.SelectedNodes();
            foreach (var item in selectedItem)
            {
                if (item.GetKind() == DoxygenDB.EntKind.PAGE)
                {
                    scene.ReplaceBookmarkItem(item.GetUniqueName());
                }
            }
            if(selectedItem.Count == 1 && selectedItem[0].GetKind() != DoxygenDB.EntKind.PAGE)
            {
                // m_refSearcher.BeginNewRefSearch();
                m_refSearcher.BeginNewNormalSearch();
                m_checkCount = 0;
            }
        }

        public void OnFindReferences(object sender, ExecutedRoutedEventArgs e)
        {
            m_refSearcher.BeginNewRefSearch();
        }
        #endregion
        
        public void CheckFindSymbolWindow(object sender, ExecutedRoutedEventArgs e)
        {
            if ((DateTime.Now - m_lastCheckRefTime).TotalMilliseconds > 3000)
            {
                //m_refSearcher.UpdateRefResult();
                m_refSearcher.UpdateNormalResult();
                m_lastCheckRefTime = DateTime.Now;
                m_checkCount++;
            }
        }

        #region Navigation
        public void OnGoUp(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(0.0, -1.0), false);
        }
        public void OnGoDown(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(0.0, 1.0), false);
        }
        public void OnGoLeft(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(-1.0, 0.0), true);
        }
        public void OnGoRight(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(1.0, 0.0), true);
        }
        public void OnGoUpInOrder(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(0.0, -1.0), true);
        }
        public void OnGoDownInOrder(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.FindNeighbour(new Vector(0.0, 1.0), true);
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
        public void OnDeleteNearbyItems(object sender, ExecutedRoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            if (scene != null)
            {
                scene.DeleteNearbyItems();
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

        #region Custom Edge
        public void OnBeginCustomEdge(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.BeginAddCustomEdge();
        }

        public void OnEndCustomEdge(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.EndAddCustomEdge();
        }
        #endregion

        #region Anchor LRU
        public void ToggleAnchor(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.ToggleAnchorItem();
        }
        #endregion

        #region Project
        public void ShowProject(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            var selectedProjects = ProjectDB.GetSelectedProject();
            scene.AcquireLock();
            foreach (var item in selectedProjects)
            {
                scene.AddProject(item);
                scene.SelectCodeItem(item);
            }
            scene.ReleaseLock();
        }

        public void ShowAllProjects(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            scene.AddAllProjectDependencies();
            scene.ReleaseLock();
        }
        #endregion

        public SymbolWindow GetSymbolWindow()
        {
            return this.symbolWindow;
        }

        bool _AnalyseSolution(bool useClang, bool onlySelectedProjects = false, bool analyse = true)
        {
            if (!GetCommandActive())
            {
                return false;
            }
            try
            {
                SetCommandActive(false);
                var traverser = new ProjectFileCollector();
                if (onlySelectedProjects)
                {
                    traverser.SetToSelectedProjects();
                }
                var scene = UIManager.Instance().GetScene();
                traverser.SetCustomExtension(scene.GetCustomExtensionDict());
                traverser.SetCustomMacro(scene.GetCustomMacroSet());
                traverser.Traverse();
                var dirList = traverser.GetDirectoryList();
                var solutionFolder = traverser.GetSolutionFolder();

                if (dirList.Count == 0 || solutionFolder == "")
                {
                    SetCommandActive(true);
                    return false;
                }
                string doxyFolder = solutionFolder + "/CodeGraphData";
                CheckOrCreateFolder(doxyFolder);

                // Use selected projects as postfix
                string postFix = "";
                if (onlySelectedProjects)
                {
                    var projectNameList = traverser.GetSelectedProjectName();
                    foreach (string item in projectNameList)
                    {
                        postFix += "_" + item;
                    }
                }
                else
                {
                    postFix = "_solution";
                }
                postFix = postFix.Replace(" ", "");

                DoxygenDB.DoxygenDBConfig config = new DoxygenDB.DoxygenDBConfig();
                config.m_configPath = doxyFolder + "/Result" + postFix + ".graph";
                config.m_inputFolders = dirList;
                config.m_outputDirectory = doxyFolder + "/Result" + postFix;
                config.m_projectName = traverser.GetSolutionName() + postFix;
                config.m_includePaths = traverser.GetAllIncludePath();
                config.m_defines = traverser.GetAllDefines();
                config.m_useClang = useClang;
                config.m_mainLanguage = traverser.GetMainLanguage();
                config.m_customExt = scene.GetCustomExtensionDict();
                DBManager.Instance().CloseDB();

                System.Threading.Thread analysisThread = new System.Threading.Thread((ThreadStart)delegate
                {
                    try
                    {
                        if(DoxygenDB.DoxygenDB.GenerateDB(config))
                        {
                            DBManager.Instance().OpenDB(config.m_configPath);
                        }
                        else
                        {
                            SetCommandActive(true);
                        }
                    }
                    catch (Exception)
                    {
                        SetCommandActive(true);
                    }
                });
                analysisThread.Name = "Analysis Thread";
                analysisThread.Start();
            }
            catch (Exception)
            {
                Logger.Warning("Analyse failed. Please try again.");
                DBManager.Instance().CloseDB();
                SetCommandActive(true);
            }
            return true;
        }

        public bool OpenDoxywizard(string configPath)
        {
            System.Threading.Thread wizardThread = new System.Threading.Thread((ThreadStart)delegate
            {
                try
                {
                    DoxygenDB.DoxygenDB.StartDoxyWizard("");
                }
                catch (Exception)
                {
                }
            });
            wizardThread.Name = "Wizard Thread";
            wizardThread.Start();
            return true;
        }

        void AnalyseSolutionButton_Click(object sender, RoutedEventArgs e)
        {
            _AnalyseSolution(true);
        }

        public void OnFastAnalyseSolutionButton(object sender, RoutedEventArgs e)
        {
            _AnalyseSolution(false);
        }

        public void OnFastAnalyseProjectsButton(object sender, RoutedEventArgs e)
        {
            _AnalyseSolution(false, true);
        }

        private void dynamicNavigationButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void staticNavigationButton_Click(object sender, RoutedEventArgs e)
        {
        }

        public bool IsDynamicNavigation()
        {
            return dynamicNavigationButton.IsChecked == true;
        }

        private void layoutButton_Click(object sender, RoutedEventArgs e)
        {
            bool isVertical = layoutGrid.RowDefinitions.Count != 0;
            if (isVertical)
            {
                layoutGrid.RowDefinitions.Clear();
                layoutGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(70, GridUnitType.Star) });
                layoutGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(5.0) });
                layoutGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(30, GridUnitType.Star) });
                splitter.Width = 5;
                splitter.Height = System.Double.NaN;
                Grid.SetRow(splitter, 0);
                Grid.SetColumn(splitter, 1);

                Grid.SetRow(tabControl, 0);
                Grid.SetColumn(tabControl, 2);
            }
            else
            {
                layoutGrid.ColumnDefinitions.Clear();
                layoutGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(70, GridUnitType.Star) });
                layoutGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(5.0) });
                layoutGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(30, GridUnitType.Star) });
                splitter.Height = 5;
                splitter.Width = System.Double.NaN;
                Grid.SetRow(splitter, 1);
                Grid.SetColumn(splitter, 0);

                Grid.SetRow(tabControl, 2);
                Grid.SetColumn(tabControl, 0);
            }
        }

        private void autoFocusButton_Click(object sender, RoutedEventArgs e)
        {
            codeView.SetAutoFocus(autoFocusButton.IsChecked);
        }

        private void lru10Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetLRULimit(15);
        }

        private void lru20Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetLRULimit(30);
        }

        private void lru50Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetLRULimit(50);
        }

        private void lru100Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetLRULimit(100);
        }

        private void lru200Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetLRULimit(200);
        }

        public void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SelectLast();
        }

        public void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SelectNext();
        }

        private void searchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var text = searchBox.Text;
                if (text == null || text == "")
                {
                    return;
                }

                var scene = UIManager.Instance().GetScene();
                scene.SelectByName(text);
            }
        }

        private void graphLayoutButton_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.m_layoutType = LayoutType.LAYOUT_GRAPH;
            scene.m_isLayoutDirty = true;
        }

        private void forceLayoutButton_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.m_layoutType = LayoutType.LAYOUT_FORCE;
            scene.m_isLayoutDirty = true;
        }

        private void noLayoutButton_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.m_layoutType = LayoutType.LAYOUT_NONE;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            scene.SaveConfig();
            scene.ReleaseLock();
        }        

        private void highLightBookmark_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetHighlightType(HighlightType.HIGHLIGHT_BOOKMARK);
        }

        private void highAllButton_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetHighlightType(HighlightType.HIGHLIGHT_ALL);
        }

        private void highLightRecent3Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetHighlightType(HighlightType.HIGHLIGHT_LATEST_3);
        }
        private void highLightRecent6Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetHighlightType(HighlightType.HIGHLIGHT_LATEST_6);
        }
        private void highLightRecent9Button_Checked(object sender, RoutedEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SetHighlightType(HighlightType.HIGHLIGHT_LATEST_9);
        }
    }
}
