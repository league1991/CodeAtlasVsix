using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// <summary>
    /// Scheme.xaml 的交互逻辑
    /// </summary>
    public partial class Scheme : UserControl
    {
        Dictionary<string, Tuple<Color, FormattedText>> m_schemeNameDict =
            new Dictionary<string, Tuple<Color, FormattedText>>();
        double m_margin = 10.0;
        double m_lineHeight = 10.0;
        double m_lineSpace = 3.0;
        //double m_colorTextSpace = 5.0;
        double m_maxTextWidth = 0.0;
        double m_fontSize = 12.0;
        double m_rectThickness = 5.0;
        List<FormattedText> m_keyText = new List<FormattedText>();
        double m_formatWidth = 0.0;
        List<Button> m_buttonList = new List<Button>();

        public Scheme()
        {
            InitializeComponent();

            ResourceSetter resMgr = new ResourceSetter(this);
            resMgr.SetStyle();

            CheckAndAddFormattedText(5);

            m_buttonList.Add(schemeButton0);
            m_buttonList.Add(schemeButton1);
            m_buttonList.Add(schemeButton2);
            m_buttonList.Add(schemeButton3);
            m_buttonList.Add(schemeButton4);
            m_buttonList.Add(schemeButton5);
            m_buttonList.Add(schemeButton6);
            m_buttonList.Add(schemeButton7);
            m_buttonList.Add(schemeButton8);
            m_buttonList.Add(schemeButton9);

            m_buttonList.Add(toggleSchemeButton0);
            m_buttonList.Add(toggleSchemeButton1);
            m_buttonList.Add(toggleSchemeButton2);
            m_buttonList.Add(toggleSchemeButton3);
            m_buttonList.Add(toggleSchemeButton4);
            m_buttonList.Add(toggleSchemeButton5);
            m_buttonList.Add(toggleSchemeButton6);
            m_buttonList.Add(toggleSchemeButton7);
            m_buttonList.Add(toggleSchemeButton8);
            m_buttonList.Add(toggleSchemeButton9);
        }

        void CheckAndAddFormattedText(int idx)
        {
            m_formatWidth = 0;
            for (int i = 0; i < idx; i++)
            {
                if (i >= m_keyText.Count)
                {
                    var formattedText = new FormattedText(string.Format("[{0}]", i + 1),
                                                            CultureInfo.CurrentUICulture,
                                                            FlowDirection.LeftToRight,
                                                            new Typeface("arial"),
                                                            m_fontSize,
                                                            Brushes.LightSalmon);
                    m_keyText.Add(formattedText);
                }
                m_formatWidth = Math.Max(m_formatWidth, m_keyText[i].Width);
            }
        }

        public void BuildSchemeLegend()
        {
            var scene = UIManager.Instance().GetScene();
            var itemDict = scene.GetItemDict();
            var schemeNameList = scene.GetCurrentSchemeList();
            var schemeColorList = scene.GetCurrentSchemeColorList();
            m_schemeNameDict.Clear();
            m_formatWidth = 0;
            CheckAndAddFormattedText(schemeNameList.Count);
            for (int i = 0; i < schemeNameList.Count; i++)
            {
                var schemeName = schemeNameList[i];
                var schemeColor = schemeColorList[i];

                var formattedText = new FormattedText(schemeName,
                                                        CultureInfo.CurrentUICulture,
                                                        FlowDirection.LeftToRight,
                                                        new Typeface("arial"),
                                                        m_fontSize,
                                                        Brushes.Moccasin);
                m_schemeNameDict[schemeName] = new Tuple<Color, FormattedText>(schemeColor, formattedText);

            }

            var nScheme = m_schemeNameDict.Count;
            //if (nScheme == 0)
            //{
            //    //this.Visibility = Visibility.Hidden;
            //    return;
            //}

            var maxWidth = 0.0;
            foreach (var item in m_schemeNameDict)
            {
                var className = item.Key;
                var textObj = item.Value.Item2;
                var classSize = new Size(textObj.Width, textObj.Height);
                maxWidth = Math.Max(maxWidth, textObj.Width);
            }

            m_maxTextWidth = maxWidth;

            this.Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                //this.MinWidth = this.Width = m_lineHeight + m_colorTextSpace + maxWidth + m_margin * 2;
                //this.MinHeight = this.Height = m_classNameDict.Count * (m_lineHeight + m_lineSpace) - m_lineSpace + m_margin * 2;
                //InvalidateArrange();
                for (int i = 0; i < m_buttonList.Count; i++)
                {
                    var button = m_buttonList[i];
                    int row = i % 10;
                    bool isToggleButton = i >= 10;
                    if (row < nScheme)
                    {
                        button.Visibility = Visibility.Visible;
                        //button.Content = schemeNameList[i];
                        //button.MinHeight = 0;
                        //button.FontSize = 12;
                        //button.Padding = new Thickness(1,-10,0,-10);
                        button.Margin = new Thickness(0,0,0,0);
                        button.BorderThickness = isToggleButton ? new Thickness(1, 1, 1, 1) : new Thickness(8,1,1,1);
                        button.Background = new SolidColorBrush();
                        //button.Foreground = Brushes.Moccasin;// new SolidColorBrush(Color.FromArgb(255,255,255,0));
                        if (!isToggleButton)
                        {
                            button.Width = m_maxTextWidth + m_formatWidth + m_buttonWidthOffset;
                            button.MaxWidth = button.Width;
                            button.MinWidth = button.Width;
                        }
                        Style style = button.TryFindResource("SchemeButtonStyle") as Style;
                        button.Style = style;
                        //button.BorderBrush = new SolidColorBrush();
                    }
                    else
                    {
                        button.Visibility = Visibility.Collapsed;
                    }
                }

                //this.MinHeight = 200;
                //this.MinWidth = 200;
                InvalidateVisual();
            });
        }

        double m_buttonWidthOffset = 18;
        double m_numberOffset = 20;
        double m_textOffset = -18;

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            var itemDict = scene.GetItemDict();

            double x = m_margin;
            double y = m_margin;
            double contentHeight = 0;
            contentHeight = (m_lineHeight + m_lineSpace) * m_schemeNameDict.Count - m_lineSpace;
            var parent = this.Parent as Grid;
            if (parent != null)
            {
                //y = parent.ActualHeight - contentHeight - m_margin;
            }
            var colorSize = new Size(m_lineHeight * 2, 2);
            double contentWidth = m_formatWidth + colorSize.Width + m_maxTextWidth + m_lineSpace;

            double buttonWidth = 19;
            if (m_schemeNameDict.Count > 0)
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), new Pen(), new Rect(new Point(x- m_rectThickness, y- m_rectThickness), new Size(contentWidth+ m_rectThickness*2 + m_lineSpace * 1.5 + buttonWidth + 6, contentHeight+ m_rectThickness*2)));
            }

            int i = 0;
            foreach (var item in m_schemeNameDict)
            {
                var className = item.Key;
                var color = item.Value.Item1;
                var textObj = item.Value.Item2;

                x = m_margin;

                dc.DrawRectangle(new SolidColorBrush(color), new Pen(), new Rect(new Point(x, y+4), colorSize));
                x += colorSize.Width + m_lineSpace * 1.3 + m_numberOffset;

                dc.DrawText(m_keyText[i], new Point(x, y-2));
                x += m_formatWidth + m_lineSpace * 0.5 + buttonWidth + m_textOffset;

                dc.DrawText(textObj,      new Point(x, y-2));
                y += m_lineHeight + m_lineSpace;
                ++i;
            }
            scene.ReleaseLock();
        }

        public void ShowScheme(int ithScheme, bool isSelected = false)
        {
            var scene = UIManager.Instance().GetScene();
            scene.ShowIthScheme(ithScheme, isSelected);
        }

        public void ToggleEdgeToScheme(int ithScheme)
        {
            var scene = UIManager.Instance().GetScene();
            scene.ToggleSelectedEdgeToScheme(ithScheme);
        }

        private void schemeButton0_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(0);
        }
        private void schemeButton1_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(1);
        }
        private void schemeButton2_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(2);
        }
        private void schemeButton3_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(3);
        }
        private void schemeButton4_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(4);
        }
        private void schemeButton5_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(5);
        }
        private void schemeButton6_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(6);
        }
        private void schemeButton7_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(7);
        }
        private void schemeButton8_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(8);
        }
        private void schemeButton9_Click(object sender, RoutedEventArgs e)
        {
            ShowScheme(9);
        }

        private void toggleSchemeButton0_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(0);
        }
        private void toggleSchemeButton1_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(1);
        }
        private void toggleSchemeButton2_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(2);
        }
        private void toggleSchemeButton3_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(3);
        }
        private void toggleSchemeButton4_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(4);
        }
        private void toggleSchemeButton5_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(5);
        }
        private void toggleSchemeButton6_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(6);
        }
        private void toggleSchemeButton7_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(7);
        }
        private void toggleSchemeButton8_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(8);
        }
        private void toggleSchemeButton9_Click(object sender, RoutedEventArgs e)
        {
            ToggleEdgeToScheme(9);
        }
    }
}
