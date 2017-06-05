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
    class ResultItem: ListBoxItem
    {
        public string m_uniqueName;

        public ResultItem(string name, string uniqueName)
        {
            this.Content = name;
            m_uniqueName = uniqueName;
        }
    }

    /// <summary>
    /// SearchWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SearchWindow : UserControl
    {
        public SearchWindow()
        {
            InitializeComponent();
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            OnSearch();
        }

        public void OnSearch()
        {
            var searchWord = nameEdit.Text;
            var searchKind = typeEdit.Text;
            var searchFile = fileEdit.Text;
            int searchLine = Convert.ToInt32(lineEdit.Text == "" ? "-1" : lineEdit.Text);
            Console.Write("------------------- Search -----------------------");
            var db = DBManager.Instance().GetDB();
            if (db == null)
            {
                return;
            }

            var ents = db.Search(searchWord, searchKind);
            resultList.Items.Clear();

            if (ents.Count == 0)
            {
                return;
            }
            var bestEntList = new List<DoxygenDB.Entity> { ents[0] };

            foreach (var entity in ents)
            {
                if (entity.Longname().Contains(searchWord))
                {
                    bestEntList.Add(entity);
                }
            }

            if (searchFile != "")
            {
                var entList = bestEntList;
                bestEntList = new List<DoxygenDB.Entity>();
                var bestEntDist = new List<int>();
                var searchWordLower = searchWord.ToLower();

                foreach (var ent in entList)
                {
                    var refs = db.SearchRef(ent.UniqueName());
                    if (refs.Count == 0)
                    {
                        continue;
                    }

                    var fileNameSet = new HashSet<string>();
                    var lineDist = int.MaxValue;
                    var hasSearchFile = false;
                    foreach (var refObj in refs)
                    {
                        if (refObj == null)
                        {
                            continue;
                        }

                        var fileEnt = refObj.File();
                        var line = refObj.Line();
                        var column = refObj.Column();
                        fileNameSet.Add(fileEnt.Longname());
                        if (fileEnt.Longname().Contains(searchFile))
                        {
                            lineDist = Math.Min(lineDist, Math.Abs(line - searchLine));
                            hasSearchFile = true;
                        }
                    }

                    foreach (var fileName in fileNameSet)
                    {
                        Console.WriteLine("file: " + fileName);
                    }

                    if (hasSearchFile && searchWordLower.Contains(ent.Name().ToLower()))
                    {
                        Console.WriteLine("In filename: " + ent.Longname() + " " + ent.Name() + " " + lineDist.ToString());
                        bestEntList.Add(ent);
                        bestEntDist.Add(lineDist);
                    }
                }

                if (searchLine > -1)
                {
                    var minDist = int.MaxValue;
                    DoxygenDB.Entity bestEnt = null;
                    for (int i = 0; i < bestEntList.Count; i++)
                    {
                        if (bestEntDist[i] < minDist)
                        {
                            minDist = bestEntDist[i];
                            bestEnt = bestEntList[i];
                        }
                    }

                    bestEntList = new List<DoxygenDB.Entity> { bestEnt };
                }
            }

            ResultItem bestItem = null;
            for (int i = 0; i < ents.Count; i++)
            {
                var ent = ents[i];
                var resItem = new ResultItem(ent.Name(), ent.UniqueName());
                if (bestEntList.Count > 0 && ent == bestEntList[0])
                {
                    bestItem = resItem;
                }
                resultList.Items.Add(resItem);
            }

            if (bestItem != null)
            {
                resultList.SelectedItem = bestItem;
            }
        }

        public void OnAddToScene()
        {
            var item = resultList.SelectedItem as ResultItem;
            if (item == null)
            {
                return;
            }

            var scene = UIManager.Instance().GetScene();

            if (scene == null)
            {
                return;
            }

            scene.AcquireLock();
            scene.AddCodeItem(item.m_uniqueName);
            scene.ClearSelection();
            scene.SelectCodeItem(item.m_uniqueName);
            scene.ReleaseLock();
        }

        private void addToSceneButton_Click(object sender, RoutedEventArgs e)
        {
            OnAddToScene();
        }

        private void nameEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnSearch();
            }
        }

        private void typeEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnSearch();
            }

        }

        private void fileEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnSearch();
            }

        }

        private void lineEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnSearch();
            }

        }

        private void lineEdit_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnSearch();
            }
        }

        private void lineEdit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (Regex.IsMatch(e.Text, @"^\d*$"))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }
    }
}
