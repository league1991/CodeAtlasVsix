using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public CodeUIItem()
        {
            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = Color.FromArgb(255, 255, 255, 0);
            this.Fill = brush;
            this.Stroke = brush;
            this.MouseDown += new MouseButtonEventHandler(MouseDownCallback);
            this.MouseUp += new MouseButtonEventHandler(MouseUpCallback);
            this.MouseMove += new MouseEventHandler(MouseMoveCallback);
            buildGeometry();
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
                Canvas.SetLeft(this, p2.X - dragStart.Value.X);
                Canvas.SetTop(this, p2.Y - dragStart.Value.Y);
            }
        }
        void MouseUpCallback(object sender, MouseEventArgs e)
        {
            dragStart = null;
            ReleaseMouseCapture();
        }

        void buildGeometry()
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
