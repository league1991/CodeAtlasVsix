using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CodeView
{
    public class CodeUIItem : Shape
    {
        public float radius = 10.0f;

        public CodeUIItem()
        {
            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = Color.FromArgb(255, 255, 255, 0);
            this.Fill = brush;
            this.Stroke = brush;
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                Point p1 = new Point(10.0d, 10.0d);
                Point p2 = new Point(this.radius, 10.0d);
                Point p3 = new Point(this.radius / 2, -this.radius);

                List<PathSegment> segments = new List<PathSegment>(3);
                segments.Add(new LineSegment(p1, true));
                segments.Add(new LineSegment(p2, true));
                segments.Add(new LineSegment(p3, true));

                List<PathFigure> figures = new List<PathFigure>(1);
                PathFigure pf = new PathFigure(p1, segments, true);
                figures.Add(pf);

                Geometry g = new PathGeometry(figures, FillRule.EvenOdd, null);

                return g;
            }
        }
    }
}
