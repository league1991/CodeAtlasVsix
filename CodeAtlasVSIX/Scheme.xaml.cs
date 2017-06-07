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
        double m_margin = 5.0;
        double m_lineHeight = 10.0;
        double m_lineSpace = 2.0;
        double m_colorTextSpace = 5.0;
        double m_maxTextWidth = 0.0;

        public Scheme()
        {
            InitializeComponent();

            ResourceSetter resMgr = new ResourceSetter(this);
            resMgr.SetStyle();
        }

        public void BuildSchemeLegend()
        {
            var scene = UIManager.Instance().GetScene();
            var itemDict = scene.GetItemDict();
            var schemeNameList = scene.GetCurrentSchemeList();
            var schemeColorList = scene.GetCurrentSchemeColorList();
            m_schemeNameDict.Clear();
            for (int i = 0; i < schemeNameList.Count; i++)
            {
                var schemeName = schemeNameList[i];
                var schemeColor = schemeColorList[i];

                var formattedText = new FormattedText(schemeName,
                                                        CultureInfo.CurrentUICulture,
                                                        FlowDirection.LeftToRight,
                                                        new Typeface("tahoma"),
                                                        10.0,
                                                        Brushes.White);
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
                InvalidateVisual();
            });
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            var itemDict = scene.GetItemDict();

            double x = m_margin, y = m_margin;
            var parent = this.Parent as Canvas;
            if (parent != null)
            {
                y = parent.ActualHeight - (m_lineHeight + m_lineSpace) * m_schemeNameDict.Count + m_lineSpace - m_margin;
            }
            var colorSize = new Size(m_lineHeight*2, 2);
            double s = 10;
            foreach (var item in m_schemeNameDict)
            {
                var className = item.Key;
                var color = item.Value.Item1;
                var textObj = item.Value.Item2;
                dc.DrawRectangle(new SolidColorBrush(color), new Pen(), new Rect(new Point(x, y+6), colorSize));
                dc.DrawText(textObj, new Point(x + m_lineHeight*2 + m_colorTextSpace, y));
                y += m_lineHeight + m_lineSpace;
            }
            scene.ReleaseLock();
        }
    }
}
