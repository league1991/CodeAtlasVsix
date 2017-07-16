using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CodeAtlasVSIX
{
    using DataDict = Dictionary<string, object>;

    public class OrderData
    {
        public int m_order;
        public Point m_point;
        public OrderData(int order, Point point)
        {
            m_order = order;
            m_point = point;
        }
    }

    public class CodeUIEdgeItem: Shape
    {
        enum StrokeMode
        {
            STROKE_SELECTED = 1,
            STROKE_CANDIDATE = 2,
            STROKE_NORMAL = 3
        };
        public string m_srcUniqueName;
        public string m_tarUniqueName;
        PathGeometry m_geometry = new PathGeometry();
        bool m_isDirty = false;
        bool m_isSelected = false;
        bool m_isMouseHover = false;
        DateTime m_mouseDownTime = new DateTime();
        public OrderData m_orderData = null;
        public bool m_isConnectedToFocusNode = false;
        Point m_p0, m_p1, m_p2, m_p3;
        bool m_isPathDirty = false;
        bool m_isCandidate = false;
        bool m_inValidating = false;
        public string m_file = "";
        public int m_line = 0;
        public int m_column = 0;
        public bool m_customEdge = false;
        public int m_selectTimeStamp = 0;
        StrokeMode m_strokeMode = StrokeMode.STROKE_NORMAL;
        List<Color> m_schemeColorList = new List<Color>();

        public CodeUIEdgeItem(string srcName, string tarName, DataDict edgeData)
        {
            m_srcUniqueName = srcName;
            m_tarUniqueName = tarName;

            if (edgeData != null)
            {
                if (edgeData.ContainsKey("dbRef"))
                {
                    var dbRef = edgeData["dbRef"] as DoxygenDB.Reference;
                    if (dbRef!= null)
                    {
                        m_file = dbRef.File().Longname();
                        m_line = dbRef.Line();
                        m_column = dbRef.Column();
                    }
                }
                if (edgeData.ContainsKey("customEdge"))
                {
                    m_customEdge = (int)edgeData["customEdge"] != 0;
                }
            }

            var scene = UIManager.Instance().GetScene();
            var srcItem = scene.GetItemDict()[srcName];
            string srcItemFile;
            int srcItemLine, srcItemColumn;
            srcItem.GetDefinitionPosition(out srcItemFile, out srcItemLine, out srcItemColumn);
            if (srcItemFile != "" &&  m_file != srcItemFile)
            {
                m_file = srcItemFile;
                m_line = srcItemLine;
                m_column = srcItemColumn;
            }

            this.MouseDown += new MouseButtonEventHandler(MouseDownCallback);
            this.MouseUp += new MouseButtonEventHandler(MouseUpCallback);
            this.MouseMove += new MouseEventHandler(MouseMoveCallback);
            this.MouseEnter += new MouseEventHandler(MouseEnterCallback);
            this.MouseLeave += new MouseEventHandler(MouseLeaveCallback);

            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(100, 150,150,150));
            this.Fill = Brushes.Transparent;
            this.Stroke = brush;
            this.StrokeThickness = 2.0;
            BuildGeometry();

            Canvas.SetZIndex(this, -1);
        }
        
        public void ClearSchemeColorList()
        {
            m_schemeColorList.Clear();
            UpdateStroke();
        }

        public void AddSchemeColor(Color color)
        {
            m_schemeColorList.Add(color);
            UpdateStroke();
        }

        Point CalculateBezierPoint(double t, Point p1, Point p2, Point p3, Point p4)
        {
            Point p = new Point();
            double tPower3 = t * t * t;
            double tPower2 = t * t;
            double oneMinusT = 1 - t;
            double oneMinusTPower3 = oneMinusT * oneMinusT * oneMinusT;
            double oneMinusTPower2 = oneMinusT * oneMinusT;
            p.X = oneMinusTPower3 * p1.X + (3 * oneMinusTPower2 * t * p2.X) + (3 * oneMinusT * tPower2 * p3.X) + tPower3 * p4.X;
            p.Y = oneMinusTPower3 * p1.Y + (3 * oneMinusTPower2 * t * p2.Y) + (3 * oneMinusT * tPower2 * p3.Y) + tPower3 * p4.Y;
            return p;
        }

        public Point PointAtPercent(double t)
        {
            return CalculateBezierPoint(t, m_p0, m_p1, m_p2, m_p3);
        }

        public Point GetMiddlePos()
        {
            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            if (srcNode == null || tarNode == null)
            {
                return new Point();
            }
            var srcPnt = srcNode.Pos;
            var tarPnt = tarNode.Pos;
            return srcPnt + (tarPnt - srcPnt) * 0.5;
        }

        public void GetNodePos(out Point srcPos, out Point tarPos)
        {
            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            if (srcNode == null || tarNode == null)
            {
                srcPos = tarPos = new Point();
            }
            srcPos = srcNode.GetRightSlotPos();
            tarPos = tarNode.GetLeftSlotPos();
            //srcPos = srcNode.Pos;
            //tarPos = tarNode.Pos;
        }

        public int ComparePos(CodeUIEdgeItem other)
        {
            if (m_line < other.m_line)
            {
                return -1;
            }
            else if (m_line > other.m_line)
            {
                return 1;
            }
            else if (m_column > other.m_column)
            {
                return -1;
            }
            else if (m_column < other.m_column)
            {
                return 1;
            }
            return 0;
        }

        public double FindCurveYPos(double x)
        {
            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            var p0 = srcNode.GetRightSlotPos();
            var p3 = tarNode.GetLeftSlotPos();
            if (p0 != m_p0 || p3 != m_p3)
            {
                m_isPathDirty = true;
            }
            m_p0 = p0;
            m_p3 = p3;
            m_p1 = new Point(m_p0.X * 0.5 + m_p3.X * 0.5, m_p0.Y);
            m_p2 = new Point(m_p0.X * 0.5 + m_p3.X * 0.5, m_p3.Y);

            var sign = 1.0;
            if (m_p3.X < m_p0.X)
            {
                sign = -1.0;
            }
            var minT = 0.0;
            var maxT = 1.0;
            var minPnt = PointAtPercent(minT);
            var maxPnt = PointAtPercent(maxT);
            for (int i = 0; i < 8; i++)
            {
                var midT = (minT + maxT) * 0.5;
                var midPnt = PointAtPercent(midT);
                if ((midPnt.X - x) * sign < 0.0)
                {
                    minT = midT;
                    minPnt = midPnt;
                }
                else
                {
                    maxT = midT;
                    maxPnt = midPnt;
                }
                if (Math.Abs(minPnt.Y - maxPnt.Y) < 0.01)
                {
                    break;
                }
            }
            return (minPnt.Y + maxPnt.Y) * 0.5;
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

        public bool IsCandidate
        {
            get { return m_isCandidate; }
            set
            {
                m_isCandidate = value;
                UpdateStroke();
            }
        }
        public bool IsSelected
        {
            get { return m_isSelected; }
            set
            {
                var oldValue = m_isSelected;
                m_isSelected = value;
                if (oldValue != value)
                {
                    UpdateStroke();
                    UIManager.Instance().GetScene().OnSelectItems();
                }
            }
        }

        public void UpdateStroke()
        {
            StrokeMode newMode = StrokeMode.STROKE_NORMAL;

            if (m_isSelected || m_isMouseHover)
            {
                newMode = StrokeMode.STROKE_SELECTED;
            }
            else if (m_isCandidate)
            {
                newMode = StrokeMode.STROKE_CANDIDATE;
            }
            else
            {
                newMode = StrokeMode.STROKE_NORMAL;
            }

            if (newMode != m_strokeMode)
            {
                m_strokeMode = newMode;

                this.Dispatcher.BeginInvoke((ThreadStart)delegate
                {
                    switch (m_strokeMode)
                    {
                        case StrokeMode.STROKE_SELECTED:
                            StrokeThickness = 5.5;
                            this.Stroke = new SolidColorBrush(Color.FromRgb(255, 157, 38));
                            Canvas.SetZIndex(this, -1);
                            break;
                        case StrokeMode.STROKE_CANDIDATE:
                            StrokeThickness = 5.5;
                            this.Stroke = new SolidColorBrush(Color.FromArgb(255, 169, 111, 42));
                            Canvas.SetZIndex(this, -2);
                            break;
                        case StrokeMode.STROKE_NORMAL:
                        default:
                            StrokeThickness = 2.0;
                            this.Stroke = new SolidColorBrush(Color.FromArgb(100, 150, 150, 150));
                            Canvas.SetZIndex(this, -2);
                            break;
                    }
                });
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
            var scene = UIManager.Instance().GetScene();
            scene.SelectOneEdge(this);
        }

        void MouseDoubleClickCallback(object sender, MouseEventArgs args)
        {
            var scene = UIManager.Instance().GetScene();
            scene.SelectOneEdge(this);
            scene.ShowInEditor();
        }

        void MouseMoveCallback(object sender, MouseEventArgs args)
        {
        }

        void MouseUpCallback(object sender, MouseEventArgs e)
        {
        }

        void MouseEnterCallback(object sender, MouseEventArgs e)
        {
            m_isMouseHover = true;
            UpdateStroke();
            //if (!IsSelected)
            //{
            //    //StrokeThickness = 2.0;
            //}
        }

        void MouseLeaveCallback(object sender, MouseEventArgs e)
        {
            m_isMouseHover = false;
            UpdateStroke();
            //if (!IsSelected)
            //{
            //    //StrokeThickness = 1.0;
            //}
        }

        void BuildGeometry()
        {
            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            //this.StartPoint = srcNode.Pos();
            //this.EndPoint = tarNode.Pos();
        }

        public int GetCallOrder()
        {
            if (m_orderData != null)
            {
                return m_orderData.m_order;
            }
            return -1;
        }

        public bool IsInvalidating()
        {
            return m_inValidating;
        }

        public void Invalidate()
        {
            if (m_inValidating)
            {
                return;
            }

            var scene = UIManager.Instance().GetScene();
            var srcNode = scene.GetNode(m_srcUniqueName);
            var tarNode = scene.GetNode(m_tarUniqueName);
            if (IsDirty || srcNode.IsDirty || tarNode.IsDirty)
            {
                m_inValidating = true;
                this.Dispatcher.BeginInvoke((ThreadStart)delegate
                {
                    this._Invalidate();
                    m_inValidating = false;
                });
            }
        }

        void _Invalidate()
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            //var beg = System.Environment.TickCount;
            base.OnRender(drawingContext);

            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();

            int nColor = m_schemeColorList.Count;
            if (nColor > 0)
            {
                var dashPattern = new List<double> { 5.0, 5.0 * (nColor - 1) };
                for (int i = 0; i < nColor; i++)
                {
                    var pen = new Pen(new SolidColorBrush(m_schemeColorList[i]), 1.5);
                    pen.DashStyle = new DashStyle(dashPattern, 0.0);
                    pen.DashStyle.Offset = 5.0 * i;
                    drawingContext.DrawGeometry(Brushes.Transparent, pen, m_geometry);
                }
            }

            if (m_orderData != null)
            {
                var formattedText = new FormattedText(m_orderData.m_order.ToString(),
                                                        CultureInfo.CurrentUICulture,
                                                        FlowDirection.LeftToRight,
                                                        new Typeface("tahoma"),
                                                        10.0,
                                                        Brushes.Black);
                var pos = new Point(m_orderData.m_point.X - formattedText.Width*0.5, m_orderData.m_point.Y - formattedText.Height * 0.5);
                
                drawingContext.DrawEllipse(this.Stroke, new Pen(), m_orderData.m_point, 6.0, 6.0);
                drawingContext.DrawText(formattedText, pos);
            }
            scene.ReleaseLock();

            //var end = System.Environment.TickCount;
            //Logger.Debug("edge render time:" + (end-beg));
        }


        protected override Geometry DefiningGeometry
        {
            get
            {
                //int begTime = System.Environment.TickCount;
                var scene = UIManager.Instance().GetScene();
                var srcNode = scene.GetNode(m_srcUniqueName);
                var tarNode = scene.GetNode(m_tarUniqueName);
                var p0 = srcNode.GetRightSlotPos();
                var p3 = tarNode.GetLeftSlotPos();
                if (p0 == m_p0 && p3 == m_p3 && m_geometry != null && !m_isPathDirty)
                {
                    return m_geometry;
                }
                m_p0 = p0;
                m_p3 = p3;
                m_p1 = new Point(m_p0.X * 0.5 + m_p3.X * 0.5, m_p0.Y);
                m_p2 = new Point(m_p0.X * 0.5 + m_p3.X * 0.5, m_p3.Y);

                var segment = new BezierSegment(m_p1, m_p2, m_p3, true);                
                var figure = new PathFigure();
                figure.StartPoint = m_p0;
                figure.Segments.Add(segment);
                figure.IsClosed = false;

                m_geometry = new PathGeometry();
                m_geometry.Figures.Add(figure);

                //int endTime = System.Environment.TickCount;
                //Logger.Debug("edge geometry time:" + (endTime-begTime));
                return m_geometry;
            }
        }
    }
}
