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
    /// Legend.xaml 的交互逻辑
    /// </summary>
    public partial class Legend : UserControl
    {
        Dictionary<string, Tuple<Color, FormattedText>> m_classNameDict = 
            new Dictionary<string, Tuple<Color, FormattedText>>();
        double m_margin = 10.0;
        double m_lineHeight = 10.0;
        double m_lineSpace = 2.0;
        double m_colorTextSpace = 5.0;
        double m_maxTextWidth = 0.0;
        double m_fontSize = 11.0;
        double m_rectThickness = 5.0;

        public Legend()
        {
            InitializeComponent();
        }

        public void BuildLegend()
        {
            var scene = UIManager.Instance().GetScene();
            var itemDict = scene.GetItemDict();
            m_classNameDict.Clear();
            foreach (var itemPair in itemDict)
            {
                var item = itemPair.Value;
                if (item.IsSelected || item.m_isConnectedToFocusNode)
                {
                    var cname = item.GetClassName();
                    if (cname == "")
                    {
                        cname = "globals";
                    }
                    var color = item.GetColor();

                    var formattedText = new FormattedText(cname,
                                                            CultureInfo.CurrentUICulture,
                                                            FlowDirection.LeftToRight,
                                                            new Typeface("tahoma"),
                                                            m_fontSize,
                                                            Brushes.Moccasin);
                    m_classNameDict[cname] = new Tuple<Color, FormattedText>(color, formattedText);
                }
            }

            var nClasses = m_classNameDict.Count;
            //if (nClasses == 0)
            //{
            //    //this.Visibility = Visibility.Hidden;
            //    return;
            //}

            var maxWidth = 0.0;
            foreach (var item in m_classNameDict)
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
            var colorSize = new Size(m_lineHeight, m_lineHeight);

            if (m_classNameDict.Count > 0)
            {
                double contentWidth = m_maxTextWidth + colorSize.Width + m_lineHeight;
                double contentHeight = (m_lineHeight + m_lineSpace) * m_classNameDict.Count - m_lineSpace;
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), new Pen(), new Rect(new Point(x - m_rectThickness, y - m_rectThickness), new Size(contentWidth + m_rectThickness*2, contentHeight + m_rectThickness*2)));
            }

            foreach (var item in m_classNameDict)
            {
                var className = item.Key;
                var color = item.Value.Item1;
                var textObj = item.Value.Item2;
                dc.DrawRectangle(new SolidColorBrush(color), new Pen(), new Rect(new Point(x, y), colorSize));
                dc.DrawText(textObj, new Point(x + m_lineHeight + m_colorTextSpace, y-2));
                y += m_lineHeight + m_lineSpace;
            }
            scene.ReleaseLock();
        }
    }
}
