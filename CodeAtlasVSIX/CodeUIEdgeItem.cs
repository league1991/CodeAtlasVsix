using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CodeAtlasVSIX
{
    public class CodeUIEdgeItem: Shape
    {
        string srcUniqueName;
        string tarUniqueName;
        PathGeometry geometry = null;

        public CodeUIEdgeItem(string srcName, string tarName)
        {
            srcUniqueName = srcName;
            tarUniqueName = tarName;

            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = Color.FromArgb(255, 255, 255, 0);
            this.Fill = brush;
            this.Stroke = brush;
            BuildGeometry();
        }

        void BuildGeometry()
        {
            var segment = new BezierSegment();
            var figure = new PathFigure();
            figure.Segments.Add(segment);
            geometry = new PathGeometry();
            geometry.Figures.Add(figure);
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                var scene = UIManager.Instance().GetScene();
                var srcNode = scene.GetNode(srcUniqueName);
                var tarNode = scene.GetNode(tarUniqueName);
                var srcPosition = srcNode.Pos();
                var tarPosition = tarNode.Pos();
                var srcCtrlPnt = new Point(srcPosition.X * 0.4 + tarPosition.X * 0.6, srcPosition.Y);
                var tarCtrlPnt = new Point(srcPosition.X * 0.6 + tarPosition.X * 0.4, tarPosition.Y);

                var segment = new BezierSegment(srcCtrlPnt, tarCtrlPnt, tarPosition, true);
                geometry.Figures[0].Segments[0] = segment;
                geometry.Figures[0].IsClosed = true;
                
                EllipseGeometry circle0 = new EllipseGeometry(srcCtrlPnt, 20.0, 20.0);
                EllipseGeometry circle1 = new EllipseGeometry(tarCtrlPnt, 20.0, 20.0);

                var group = new GeometryGroup();
                group.Children.Add(circle0);
                group.Children.Add(circle1);
                //group.Children.Add(geometry);
                return group;
            }
        }
    }
}
