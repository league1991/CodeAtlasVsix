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
using Microsoft.VisualStudio.Shell;

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

            ResourceSetter resMgr = new ResourceSetter(this);
            resMgr.SetStyle();
        }


        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            OnSearch();
        }

        public void OnSearch()
        {
            var searchWord = nameEdit.Text;
            var searchKind = typeEdit.Text;
            var searchFile = fileEdit.Text.Replace("\\","/");
            int searchLine = Convert.ToInt32(lineEdit.Text == "" ? "-1" : lineEdit.Text);
            resultList.Items.Clear();
            Logger.Debug("------------------- Search -----------------------");
            var db = DBManager.Instance().GetDB();
            if (db == null)
            {
                return;
            }

            List<DoxygenDB.Entity> bestEntList;
            DoxygenDB.Entity bestEnt;
            db.SearchAndFilter(searchWord, searchKind, searchFile, searchLine, out bestEntList, out bestEnt, false);

            ResultItem bestItem = null;
            for (int i = 0; i < bestEntList.Count; i++)
            {
                var ent = bestEntList[i];
                var resItem = new ResultItem(ent.Longname(), ent.UniqueName());
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
