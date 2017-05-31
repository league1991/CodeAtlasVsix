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

    class SchemeItem : ListBoxItem
    {
        public string m_uniqueName;

        public SchemeItem(string uniqueName)
        {
            this.Content = uniqueName;
            m_uniqueName = uniqueName;
        }
    }

    /// <summary>
    /// SchemeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SchemeWindow : UserControl
    {
        public SchemeWindow()
        {
            InitializeComponent();
        }

        void OnTextEdited()
        {
            UpdateScheme();
        }

        void OnSchemeChanged(CodeUIItem currentItem, string prevItem)
        {
            if (currentItem != null)
            {
                nameEdit.Text = currentItem.GetUniqueName();
            }
        }

        void OnAddOrModifyScheme()
        {
            var schemeName = nameEdit.Text;
            if (schemeName == null)
            {
                return;
            }

            var scene = UIManager.Instance().GetScene();
            var schemeNameList = scene.GetSchemeNameList();
            var isAdd = true;
            if (schemeNameList.Contains(schemeName))
            {
                var result = MessageBox.Show(string.Format("{0} aleardy exists. Replace it?", schemeName), "Add Scheme", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.Cancel)
                {
                    isAdd = false;
                }
            }

            if (isAdd)
            {
                scene.AddOrReplaceScheme(schemeName);
                UpdateScheme();
            }
        }

        void OnShowScheme()
        {
            var item = schemeList.SelectedItem as SchemeItem;
            if (item == null)
            {
                MessageBox.Show("Please select an item to show.", "Show Scheme");
                return;
            }

            var schemeName = item.m_uniqueName;
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            scene.ShowScheme(schemeName, true);
            scene.ReleaseLock();
            UpdateScheme();
        }

        void OnDeleteScheme()
        {

            var item = schemeList.SelectedItem as SchemeItem;
            if (item == null)
            {
                MessageBox.Show("Please select an item to delete.", "Delete Scheme");
                return;
            }

            var schemeName = item.m_uniqueName;
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            scene.DeleteScheme(schemeName);
            scene.ReleaseLock();
            UpdateScheme();
        }

        void UpdateScheme()
        {
            var scene = UIManager.Instance().GetScene();
            var nameList = scene.GetSchemeNameList();
            var filter = filterEdit.Text.ToLower();

            schemeList.Items.Clear();
            foreach (var name in nameList)
            {
                if (name.ToLower().Contains(filter))
                {
                    schemeList.Items.Add(new SchemeItem(name));
                }
            }
        }

        private void showSchemeButton_Click(object sender, RoutedEventArgs e)
        {
            OnShowScheme();
        }

        private void addSchemeButton_Click(object sender, RoutedEventArgs e)
        {
            OnAddOrModifyScheme();
        }

        private void deleteSchemeButton_Click(object sender, RoutedEventArgs e)
        {
            OnDeleteScheme();
        }
    }
}
