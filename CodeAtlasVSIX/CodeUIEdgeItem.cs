using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CodeAtlasVSIX
{
    public class CodeUIEdgeItem: Shape
    {
        string m_srcUniqueName;
        string m_tarUniqueName;
        PathGeometry m_geometry = new PathGeometry();
        bool m_isDirty = false;

        public CodeUIEdgeItem(string srcName, string tarName)
        {
            m_srcUniqueName = srcName;
            m_tarUniqueName = tarName;

            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = Color.FromArgb(255, 255, 255, 0);
            this.Fill = Brushes.Transparent;
            this.Stroke = brush;
            BuildGeometry();
        }

        public bool IsDirty
        {
            get { return m_isDirty; }
            set
            {
                m_isDirty = value;
                if (value == true)
                {
                    UIManager.Instance().GetScene().Invalidate();
                }
            }
        }

        //public Point StartPoint
        //{
        //    set { SetValue(StartPointProperty, value); }
        //    get { return (Point)GetValue(StartPointProperty); }
        //}

        //public Point EndPoint
        //{
        //    set { SetValue(EndPointProperty, value); }
        //    get { return (Point)GetValue(EndPointProperty); }
        //}

        //public static readonly DependencyProperty StartPointProperty =
        //    DependencyProperty.Register("StartPoint", typeof(Point), typeof(CodeUIEdgeItem),
        //        new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender));

        //public static readonly DependencyProperty EndPointProperty =
        //    DependencyProperty.Register("EndPoint", typeof(Point), typeof(CodeUIEdgeItem),
        //        new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender));

        void BuildGeometry()
        {
            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            //this.StartPoint = srcNode.Pos();
            //this.EndPoint = tarNode.Pos();
        }

        public void Invalidate()
        {
            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            if (IsDirty || srcNode.IsDirty || tarNode.IsDirty)
            {
                this.Dispatcher.Invoke((ThreadStart)delegate
                {
                    this._Invalidate();
                });
            }
        }

        void _Invalidate()
        {
            InvalidateVisual();
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                //EllipseGeometry circle0 = new EllipseGeometry(srcCtrlPnt, 20.0, 20.0);
                //EllipseGeometry circle1 = new EllipseGeometry(tarCtrlPnt, 20.0, 20.0);

                //var group = new GeometryGroup();
                //group.Children.Add(circle0);
                //group.Children.Add(circle1);
                //group.Children.Add(geometry);
                //return group;

                var scene = UIManager.Instance().GetScene();
                var srcNode = scene.GetNode(m_srcUniqueName);
                var tarNode = scene.GetNode(m_tarUniqueName);
                var srcPosition = srcNode.Pos();
                var tarPosition = tarNode.Pos();
                var srcCtrlPnt = new Point(srcPosition.X * 0.4 + tarPosition.X * 0.6, srcPosition.Y);
                var tarCtrlPnt = new Point(srcPosition.X * 0.6 + tarPosition.X * 0.4, tarPosition.Y);

                var segment = new BezierSegment(srcCtrlPnt, tarCtrlPnt, tarPosition, true);
                var figure = new PathFigure();
                figure.StartPoint = srcPosition;
                figure.Segments.Add(segment);
                figure.IsClosed = false;
                m_geometry = new PathGeometry();
                m_geometry.Figures.Add(figure);

                return m_geometry;
            }
        }
    }
}
