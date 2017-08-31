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

    class ExtensionItem : ListBoxItem
    {
        public string m_language;
        public string m_extension;

        public ExtensionItem(string extension, string language)
        {
            this.Content = extension + " : " + language;
            m_language = language;
            m_extension = extension;
        }
    }

    /// <summary>
    /// Interaction logic for AnalysisWindow.xaml
    /// </summary>
    public partial class AnalysisWindow : UserControl
    {
        public AnalysisWindow()
        {
            InitializeComponent();
        }

        public void InitLanguageOption()
        {
            languageEdit.Items.Clear();
            var langList = new List<string> {
                "C++","Java","Javascript","C#",
                "C","D","PHP","Objective-C","Python",
                "Fortran","VHDL","IDL"};

            foreach (var item in langList)
            {
                languageEdit.Items.Add(item);
            }

        }

        private void addExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            var ext = extensionEdit.Text;
            var lang = languageEdit.Text;
            if (ext == "" || lang == "")
            {
                return;
            }

            var scene = UIManager.Instance().GetScene();
            scene.AddCustomExtension(ext, lang);
            UpdateExtensionList();
        }

        private void deleteExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            var item = resultList.SelectedItem as ExtensionItem;
            if (item == null)
            {
                MessageBox.Show("Please select an item to delete.", "Delete Extension");
                return;
            }
            
            var scene = UIManager.Instance().GetScene();
            scene.DeleteCustomExtension(item.m_extension);
            UpdateExtensionList();
        }

        public void UpdateExtensionList()
        {
            var scene = UIManager.Instance().GetScene();
            var extDict = scene.GetCustomExtensionDict();

            resultList.Items.Clear();
            foreach (var item in extDict)
            {
                resultList.Items.Add(new ExtensionItem(item.Key, item.Value));
            }
        }

        private void analyseSolutionButton_Click(object sender, RoutedEventArgs e)
        {
            UIManager.Instance().GetMainUI().OnFastAnalyseSolutionButton(null, null);
        }

        private void analyseSelectedProjButton_Click(object sender, RoutedEventArgs e)
        {
            UIManager.Instance().GetMainUI().OnFastAnalyseProjectsButton(null, null);
        }
    }
}
