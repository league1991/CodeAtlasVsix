using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

    class ForbiddenItem : ListBoxItem
    {
        public string m_uniqueName;
        public string m_name;

        public ForbiddenItem(string name, string uniqueName)
        {
            this.Content = name;
            m_name = name;
            m_uniqueName = uniqueName;
        }
    }
    /// <summary>
    /// SymbolWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SymbolWindow : UserControl
    {
        public SymbolWindow()
        {
            InitializeComponent();

            ResourceSetter resMgr = new ResourceSetter(this);
            resMgr.SetStyle();
        }

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateForbiddenSymbol();
        }

        private void OnUpdateComment(object sender, RoutedEventArgs e)
        {
            UpdateComment();
        }

        private void addSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            OnAddForbidden();
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            onDeleteForbidden();
        }

        void OnTextEdited()
        {
            UpdateForbiddenSymbol();
        }

        void OnAddForbidden()
        {
            var scene = UIManager.Instance().GetScene();
            scene.AddForbiddenSymbol();
            UpdateForbiddenSymbol();
        }

        public void UpdateForbiddenSymbol()
        {
            this.Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                var scene = UIManager.Instance().GetScene();
                var forbidden = scene.GetForbiddenSymbol();
                var filter = filterEdit.Text.ToLower();

                forbiddenList.Items.Clear();
                var itemList = new List<ForbiddenItem>();
                foreach (var item in forbidden)
                {
                    var uname = item.Key;
                    var name = item.Value;
                    if (name.ToLower().Contains(filter))
                    {
                        itemList.Add(new ForbiddenItem(name, uname));
                    }
                }
                itemList.Sort((x, y) => x.m_name.CompareTo(y.m_name));
                foreach (var item in itemList)
                {
                    forbiddenList.Items.Add(item);
                }
            });
        }

        void onDeleteForbidden()
        {
            var item = forbiddenList.SelectedItem as ForbiddenItem;

            var scene = UIManager.Instance().GetScene();
            if (item == null || scene == null)
            {
                return;
            }

            scene.AcquireLock();
            scene.DeleteForbiddenSymbol(item.m_uniqueName);
            UpdateForbiddenSymbol();
            scene.ReleaseLock();
        }

        public void UpdateSymbol(string symbolName, string comment = "")
        {
            this.Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                symbolLabel.Content = symbolName;
                commentEdit.Text = comment;
            });
        }

        void UpdateComment()
        {
            var text = commentEdit.Text;
            var scene = UIManager.Instance().GetScene();
            scene.UpdateSelectedComment(text);
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
                text = text.ToLower();

                var scene = UIManager.Instance().GetScene();
                var items = scene.GetItemDict();
                var keyList = new List<string>(items.Keys);
                var selectedList = scene.SelectedNodes();
                int startIdx = 0;
                int count = keyList.Count;
                if (selectedList.Count > 0)
                {
                    var firstUname = selectedList[0].GetUniqueName();
                    int firstIdx = keyList.IndexOf(firstUname);
                    if (firstIdx != -1)
                    {
                        startIdx = firstIdx+1;
                        count = keyList.Count - 1;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    int idx = (startIdx + i) % keyList.Count;
                    var itemUname = keyList[idx];
                    var item = items[itemUname];
                    if (item.GetName().ToLower().Contains(text) ||
                        item.GetCommentText().ToLower().Contains(text))
                    {
                        scene.SelectCodeItem(itemUname);
                        break;
                    }
                }
            }
        }
    }
}
