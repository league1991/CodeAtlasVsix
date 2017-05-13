using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CodeAtlasVSIX
{
    public class CodeUIItem : Shape
    {
        public float radius = 10.0f;
        public int nCallers = 0;
        public int nCallees = 0;

        private Nullable<Point> dragStart = null;
        private GeometryGroup geometry = null;
        private string m_uniqueName = "";
        private bool m_isDirty = false;

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

        public CodeUIItem(string uniqueName)
        {
            m_uniqueName = uniqueName;
            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = Color.FromArgb(255, 255, 255, 0);
            this.Fill = brush;
            this.Stroke = brush;
            this.MouseDown += new MouseButtonEventHandler(MouseDownCallback);
            this.MouseUp += new MouseButtonEventHandler(MouseUpCallback);
            this.MouseMove += new MouseEventHandler(MouseMoveCallback);
            BuildGeometry();
        }

    //    public Point LeftPoint
    //    {
    //        set { SetValue(LeftPointProperty, value); }
    //        get { return (Point)GetValue(LeftPointProperty); }
    //    }

    //    public Point RightPoint
    //    {
    //        set { SetValue(RightPointProperty, value); }
    //        get { return (Point)GetValue(RightPointProperty); }
    //    }

    //    public static readonly DependencyProperty LeftPointProperty =
    //DependencyProperty.Register("LeftPoint", typeof(Point), typeof(CodeUIItem),
    //    new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender));

    //    public static readonly DependencyProperty RightPointProperty =
    //        DependencyProperty.Register("RightPoint", typeof(Point), typeof(CodeUIItem),
    //            new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender));


        public Point Pos()
        {
            var pnt = this.TranslatePoint(new Point(), (UIElement)this.Parent);
            return pnt;
        }

        public void Invalidate()
        {
            if (m_isDirty)
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

        public void SetPos(Point point)
        {
            Canvas.SetLeft(this, point.X);
            Canvas.SetTop(this, point.Y);
            //LeftPoint = point;
            //RightPoint = point;
            IsDirty = true;
        }

        UIElement GetCanvas()
        {
            return (UIElement)this.Parent;
        }

        void MouseDownCallback(object sender, MouseEventArgs args)
        {
            dragStart = args.GetPosition(this);
            CaptureMouse();
        }

        void MouseMoveCallback(object sender, MouseEventArgs args)
        {
            if (dragStart != null && args.LeftButton == MouseButtonState.Pressed)
            {
                var canvas = GetCanvas();
                var p2 = args.GetPosition(canvas);
                SetPos(new Point(p2.X - dragStart.Value.X, p2.Y - dragStart.Value.Y));
                //Canvas.SetLeft(this, p2.X - dragStart.Value.X);
                //Canvas.SetTop(this, p2.Y - dragStart.Value.Y);
            }
        }
        void MouseUpCallback(object sender, MouseEventArgs e)
        {
            dragStart = null;
            ReleaseMouseCapture();
        }

        void BuildGeometry()
        {
            EllipseGeometry circle = new EllipseGeometry(new Point(0.0, 0.0), radius, radius);

            geometry = new GeometryGroup();
            geometry.Children.Add(circle);
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                return geometry;
            }
        }
    }
}
