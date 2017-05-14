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
        public float m_radius = 10.0f;
        public int nCallers = 0;
        public int nCallees = 0;

        private Nullable<Point> dragStart = null;
        private GeometryGroup geometry = null;
        private string m_uniqueName = "";
        private bool m_isDirty = false;
        private Point m_targetPos = new Point();
        private DateTime m_mouseDownTime = new DateTime();
        private bool m_isSelected = false;
        private Point m_position = new Point();

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
            this.Stroke = Brushes.Transparent;
            this.MouseDown += new MouseButtonEventHandler(MouseDownCallback);
            this.MouseUp += new MouseButtonEventHandler(MouseUpCallback);
            this.MouseMove += new MouseEventHandler(MouseMoveCallback);
            this.MouseEnter += new MouseEventHandler(MouseEnterCallback);
            this.MouseLeave += new MouseEventHandler(MouseLeaveCallback);
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

        public bool IsSelected
        {
            get { return m_isSelected; }
            set
            {
                m_isSelected = value;
                if(m_isSelected)
                {
                    this.Stroke = Brushes.Tomato;
                }
                else
                {
                    this.Stroke = Brushes.Transparent;
                }
            }
        }

        void _Invalidate()
        {
            InvalidateVisual();
        }

        public Point Pos
        {
            set
            {
                //if (this.Dispatcher.CheckAccess())
                //{
                //    Canvas.SetLeft(this, value.X);
                //    Canvas.SetTop(this, value.Y);
                //}
                //else
                //{
                //    this.Dispatcher.Invoke(() => {
                //    Canvas.SetLeft(this, value.X);
                //    Canvas.SetTop(this, value.Y); });
                //}
                Canvas.SetLeft(this, value.X);
                Canvas.SetTop(this, value.Y);
                //LeftPoint = point;
                //RightPoint = point;
                m_position = value;
                IsDirty = true;
            }
            get
            {
                //var pnt = this.TranslatePoint(new Point(), (UIElement)this.Parent);
                return m_position;
            }
        }

        public double GetRadius()
        {
            // TODO: more code
            return m_radius;
        }

        public void SetTargetPos(Point point)
        {
            m_targetPos = point;
        }

        public void MoveToTarget(double ratio)
        {
            double pntX = Pos.X;
            double pntY = Pos.Y;
            pntX = pntX * (1.0 - ratio) + m_targetPos.X * ratio;
            pntY = pntY * (1.0 - ratio) + m_targetPos.Y * ratio;
            Pos = new Point(pntX, pntY);
        }

        UIElement GetCanvas()
        {
            return (UIElement)this.Parent;
        }

        #region mouse callback
        void MouseDownCallback(object sender, MouseEventArgs args)
        {
            var newDownTime = DateTime.Now;
            double duration = (newDownTime - m_mouseDownTime).TotalMilliseconds;
            m_mouseDownTime = newDownTime;
            if (duration > System.Windows.Forms.SystemInformation.DoubleClickTime)
            {
                MouseClickCallback(sender, args);
            }
            else
            {
                MouseDoubleClickCallback(sender, args);
            }
        }

        void MouseClickCallback(object sender, MouseEventArgs args)
        {
            IsSelected = true;
            dragStart = args.GetPosition(this);
            CaptureMouse();
        }

        void MouseDoubleClickCallback(object sender, MouseEventArgs args)
        {
            IsSelected = true;
            System.Console.Out.WriteLine("double click");
        }

        void MouseMoveCallback(object sender, MouseEventArgs args)
        {
            if (dragStart != null && args.LeftButton == MouseButtonState.Pressed)
            {
                var canvas = GetCanvas();
                var p2 = args.GetPosition(canvas);
                Pos = new Point(p2.X - dragStart.Value.X, p2.Y - dragStart.Value.Y);
                SetTargetPos(Pos);
                //Canvas.SetLeft(this, p2.X - dragStart.Value.X);
                //Canvas.SetTop(this, p2.Y - dragStart.Value.Y);
            }
        }

        void MouseUpCallback(object sender, MouseEventArgs e)
        {
            dragStart = null;
            ReleaseMouseCapture();
        }

        void MouseEnterCallback(object sender, MouseEventArgs e)
        {
            if (IsSelected)
            {

            }
            else
            {
                this.Stroke = Brushes.Tomato;
            }
        }

        void MouseLeaveCallback(object sender, MouseEventArgs e)
        {
            if(IsSelected)
            {

            }
            else
            {
                Stroke = Brushes.Transparent;
            }
        }
        #endregion

        void BuildGeometry()
        {
            EllipseGeometry circle = new EllipseGeometry(new Point(0.0, 0.0), m_radius, m_radius);

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
