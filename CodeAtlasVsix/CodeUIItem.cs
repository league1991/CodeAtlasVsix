using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        class Variant
        {
            public Variant(string str) { m_string = str; }
            public Variant(int val) { m_int = val; }
            public Variant(double val) { m_double = val; }
            public string m_string;
            public int m_int;
            public double m_double;
        }
        
        public int nCallers = 0;
        public int nCallees = 0;

        Nullable<Point> dragStart = null;
        GeometryGroup m_geometry = null;
        string m_uniqueName = "";
        bool m_isDirty = false;
        Point m_targetPos = new Point();
        DateTime m_mouseDownTime = new DateTime();
        bool m_isSelected = false;
        bool m_isHover = false;
        Point m_position = new Point();
        public int m_selectCounter = 0;
        public double m_selectTimeStamp = 0;

        string m_name = "";
        string m_displayName = "";
        int m_lines = 0;
        string m_kindName = "";
        DoxygenDB.EntKind m_kind = DoxygenDB.EntKind.UNKNOWN;
        static FontFamily s_titleFont = new FontFamily("tahoma");
        Size m_fontSize = new Size();
        Size m_commentSize = new Size();
        double m_lineHeight = 0.0;
        bool m_isConnectedToFocusNode = false;
        Dictionary<string, Variant> m_customData = new Dictionary<string, Variant>();
        Color m_color = new Color();

        public CodeUIItem(string uniqueName)
        {
            m_uniqueName = uniqueName;
            this.MouseDown += new MouseButtonEventHandler(MouseDownCallback);
            this.MouseUp += new MouseButtonEventHandler(MouseUpCallback);
            this.MouseMove += new MouseEventHandler(MouseMoveCallback);
            this.MouseEnter += new MouseEventHandler(MouseEnterCallback);
            this.MouseLeave += new MouseEventHandler(MouseLeaveCallback);

            var dbObj = DBManager.Instance().GetDB();
            var scene = UIManager.Instance().GetScene();
            var entity = dbObj.SearchFromUniqueName(uniqueName);

            if (entity != null)
            {
                this.ToolTip = entity.Longname();
                m_name = entity.Name();
                BuildDisplayName(m_name);
                var comment = scene.GetComment(m_uniqueName);
                BuildCommentSize(comment);
                m_kindName = entity.KindName();
                var metricRes = entity.Metric();
                if (metricRes.ContainsKey("CountLine"))
                {
                    var metricLine = metricRes["CountLine"].m_int;
                    m_lines = metricLine;
                }
            }

            var kindStr = m_kindName.ToLower();
            // custom data

            if (kindStr.Contains("function") || kindStr.Contains("method"))
            {
                m_kind = DoxygenDB.EntKind.FUNCTION;
                // Find caller and callee count
                var callerList = new List<DoxygenDB.Entity>();
                var callerRefList = new List<DoxygenDB.Reference>();
                var calleeList = new List<DoxygenDB.Entity>();
                var calleeRefList = new List<DoxygenDB.Reference>();
                dbObj.SearchRefEntity(out callerList, out callerRefList, m_uniqueName, "callby", "function, method", true);
                dbObj.SearchRefEntity(out calleeList, out calleeRefList, m_uniqueName, "call", "function, method", true);
                m_customData.Add("nCaller", new Variant(callerList.Count));
                m_customData.Add("nCallee", new Variant(calleeList.Count));
                m_customData.Add("callerR", new Variant(GetCallerRadius(callerList.Count)));
                m_customData.Add("calleeR", new Variant(GetCallerRadius(calleeList.Count)));
            }
            else if (kindStr.Contains("attribute") || kindStr.Contains("variable") ||
                kindStr.Contains("object"))
            {
                m_kind = DoxygenDB.EntKind.VARIABLE;
            }
            else if (kindStr.Contains("class") || kindStr.Contains("struct"))
            {
                m_kind = DoxygenDB.EntKind.CLASS;
            }
            else
            {
                m_kind = DoxygenDB.EntKind.UNKNOWN;
            }

            if (m_kind == DoxygenDB.EntKind.FUNCTION || m_kind == DoxygenDB.EntKind.VARIABLE)
            {
                if (entity == null)
                {
                    m_color = Color.FromRgb(195, 195, 195);
                }
                else
                {
                    List<DoxygenDB.Entity> defineList;
                    List<DoxygenDB.Reference> defineRefList;
                    dbObj.SearchRefEntity(out defineList, out defineRefList, uniqueName, "definein");
                    var name = "";
                    var hasDefinition = true;
                    if (defineList.Count == 0)
                    {
                        dbObj.SearchRefEntity(out defineList, out defineRefList, uniqueName, "declarein");
                        hasDefinition = false;
                    }
                    m_customData.Add("hasDef", new Variant(hasDefinition ? 1 : 0));
                    if (defineList.Count != 0)
                    {
                        foreach (var defineEnt in defineList)
                        {
                            if (defineEnt.KindName().ToLower().Contains("class") ||
                                defineEnt.KindName().ToLower().Contains("struct"))
                            {
                                name = defineEnt.Name();
                                m_customData.Add("className", new Variant(name));
                                break;
                            }
                        }
                    }
                    m_color = NameToColor(name);
                }
            }
            else if (m_kind == DoxygenDB.EntKind.CLASS)
            {
                m_color = NameToColor(m_name);
            }

            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = m_color;
            this.Fill = brush;
            this.Stroke = Brushes.Transparent;
            BuildGeometry();
             
            Canvas.SetZIndex(this, 0);
        }

        public Color GetColor()
        {
            return m_color;
        }

        public string GetClassName()
        {
            if (m_kind == DoxygenDB.EntKind.CLASS)
            {
                return m_name;
            }
            return m_customData["className"].m_string;
        }

        void BuildDisplayName(string name)
        {
            string pattern = @"([A-Z]*[a-z0-9]*_*~*)";
            var nameList = Regex.Matches(
                name,
                pattern,
                RegexOptions.ExplicitCapture
                );

            var partLength = 0;
            m_displayName = "";

            foreach (Match nextMatch in nameList)
            {
                m_displayName += nextMatch.Value;
                partLength += nextMatch.Value.Length;
                if (partLength > 13)
                {
                    m_displayName += "\n";
                    partLength = 0;
                }
            }
            m_displayName = m_displayName.Trim();
            int nLine = m_displayName.Count(f => f == '\n') + 1;

            var formattedText = new FormattedText(m_displayName,
                                                    CultureInfo.CurrentUICulture,
                                                    FlowDirection.LeftToRight,
                                                    new Typeface("tahoma"),
                                                    8.0,
                                                    Brushes.Black);
            m_fontSize = new Size(formattedText.Width, formattedText.Height);
            m_lineHeight = formattedText.LineHeight;
        }

        void BuildCommentSize(string comment)
        {
            if (comment == "")
            {
                m_commentSize = new Size();
            }

            var formattedText = new FormattedText(comment,
                                                    CultureInfo.CurrentUICulture,
                                                    FlowDirection.LeftToRight,
                                                    new Typeface("tahoma"),
                                                    8.0,
                                                    Brushes.Black);
            var lines = (int)(formattedText.Width / 100.0);
            m_commentSize = new Size(100, (formattedText.LineHeight * lines - formattedText.OverhangLeading));
        }

        public bool IsFunction()
        {
            return m_kind == DoxygenDB.EntKind.FUNCTION;
        }

        double GetCallerRadius(int num)
        {
            return Math.Log((double)num + 1.0) * 5.0;
        }

        static public Color HSLToRGB(double H, double S, double L)
        {
            double v;
            double r, g, b;

            r = L;   // default to gray
            g = L;
            b = L;
            v = (L <= 0.5) ? (L * (1.0 + S)) : (L + S - L * S);
            if (v > 0)
            {
                double m;
                double sv;
                int sextant;
                double fract, vsf, mid1, mid2;

                m = L + L - v;
                sv = (v - m) / v;
                H *= 6.0;
                sextant = (int)H;
                fract = H - sextant;
                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;
                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;
                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;
                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;
                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;
                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }
            Color rgb = new Color();
            rgb.R = Convert.ToByte(r * 255.0f);
            rgb.G = Convert.ToByte(g * 255.0f);
            rgb.B = Convert.ToByte(b * 255.0f);
            rgb.A = 255;
            return rgb;
        }

        static Color NameToColor(string name)
        {
            uint hashVal = (uint)name.GetHashCode();
            var h = (hashVal & 0xff) / 255.0;
            var s = ((hashVal >> 8) & 0xff) / 255.0;
            var l = ((hashVal >> 16) & 0xff) / 255.0;
            return HSLToRGB(h, 0.35 + s * 0.3, 0.4 + l * 0.15);
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
                    this.StrokeThickness = 1.5;
                }
                else
                {
                    this.Stroke = Brushes.Transparent;
                    this.StrokeThickness = 1.0;
                }
                UIManager.Instance().GetScene().OnSelectItems();
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
            var r = 8.0;
            if (m_kind != DoxygenDB.EntKind.VARIABLE)
            {
                r = Math.Pow((double)(m_lines + 1), 0.3) * 5.0;
            }
            if (IsFunction())
            {
                r = Math.Max(r, m_customData["callerR"].m_double * 0.4);
                r = Math.Max(r, m_customData["calleeR"].m_double * 0.4);
            }
            return r;
        }

        public double GetWidth()
        {
            var r = GetRadius() * 2.0;
            if (IsFunction())
            {
                r += m_customData["callerR"].m_double + m_customData["calleeR"].m_double;
            }
            return r;
        }

        public double GetBodyRadius()
        {
            double r = 8.0;
            if (m_kind != DoxygenDB.EntKind.VARIABLE)
            {
                r = Math.Pow((double)(m_lines + 1), 0.3) * 5.0;
            }
            return r;
        }

        public double GetHeight()
        {
            double h = (m_fontSize.Height + m_commentSize.Height) * 1.67;
            return h;
        }

        public Point GetLeftSlotPos()
        {
            var l = GetBodyRadius();
            if (IsFunction())
            {
                l += m_customData["callerR"].m_double;
            }
            var pos = Pos;
            return new Point(pos.X - l, pos.Y);
        }

        public Point GetRightSlotPos()
        {
            var l = GetBodyRadius();
            if (IsFunction())
            {
                l += m_customData["calleeR"].m_double;
            }
            var pos = Pos;
            return new Point(pos.X + l, pos.Y);
        }

        public void SetTargetPos(Point point)
        {
            m_targetPos = point;
        }

        public Vector DispToTarget()
        {
            return m_targetPos - Pos;
        }

        public void MoveToTarget(double ratio)
        {
            double pntX = Pos.X;
            double pntY = Pos.Y;
            pntX = pntX * (1.0 - ratio) + m_targetPos.X * ratio;
            pntY = pntY * (1.0 - ratio) + m_targetPos.Y * ratio;
            Pos = new Point(pntX, pntY);
        }

        public DoxygenDB.EntKind GetKind()
        {
            return m_kind;
        }

        public string GetUniqueName()
        {
            return m_uniqueName;
        }

        public DoxygenDB.Entity GetEntity()
        {
            return DBManager.Instance().GetDB().SearchFromUniqueName(m_uniqueName);
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
            m_geometry = new GeometryGroup();
            var r = GetBodyRadius();

            if (IsFunction())
            {
                var cosRadian = Math.Cos(20.0 / 180.0 * Math.PI);
                var sinRadian = Math.Sin(20.0 / 180.0 * Math.PI);

                var r0 = r + 1.0;
                if (m_customData["nCaller"].m_int > 0)
                {
                    var cr = m_customData["callerR"].m_double;
                    
                    var figure = new PathFigure();
                    figure.StartPoint = new Point(-r0, 0);
                    figure.Segments.Add(new LineSegment(new Point(-r0 - cr * cosRadian, cr * sinRadian), true));
                    //figure.Segments.Add(new LineSegment(new Point(-r0 - cr * cosRadian, -sinRadian), true));
                    figure.Segments.Add(new ArcSegment(new Point(-r0 - cr * cosRadian, cr * -sinRadian), new Size(cr, cr), 0.0, false, SweepDirection.Clockwise, true));
                    figure.IsClosed = true;
                    figure.IsFilled = true;
                    var pathGeo = new PathGeometry();
                    pathGeo.Figures.Add(figure);
                    m_geometry.Children.Add(pathGeo);
                }
                if (m_customData["nCallee"].m_int > 0)
                {
                    var cr = m_customData["calleeR"].m_double;

                    var figure = new PathFigure();
                    figure.StartPoint = new Point(r0, 0);
                    figure.Segments.Add(new LineSegment(new Point(r0 + cr * cosRadian, cr * sinRadian), true));
                    figure.Segments.Add(new ArcSegment(new Point(r0 + cr * cosRadian, cr * -sinRadian), new Size(cr, cr), 0.0, false, SweepDirection.Counterclockwise, true));
                    figure.IsClosed = true;
                    figure.IsFilled = true;
                    var pathGeo = new PathGeometry();
                    pathGeo.Figures.Add(figure);
                    m_geometry.Children.Add(pathGeo);
                }

                Geometry circle = new EllipseGeometry(new Point(0.0, 0.0), r, r);
                if (m_lines == 0 || m_customData["hasDef"].m_int == 0)
                {
                    var innerCircle = new EllipseGeometry(new Point(0.0, 0.0), 2.5, 2.5);
                    circle = Geometry.Combine(circle, innerCircle, GeometryCombineMode.Exclude, null);
                }
                m_geometry.Children.Add(circle);
            }
            else if (m_kind == DoxygenDB.EntKind.VARIABLE)
            {
                var figure = new PathFigure();
                figure.StartPoint = new Point(-r, 0.0);
                figure.Segments.Add(new LineSegment(new Point(r, r), true));
                figure.Segments.Add(new LineSegment(new Point(r, -r), true));
                figure.IsClosed = true;
                figure.IsFilled = true;
                var pathGeo = new PathGeometry();
                pathGeo.Figures.Add(figure);
                m_geometry.Children.Add(pathGeo);
            }
            else if(m_kind == DoxygenDB.EntKind.CLASS)
            {
                var rect = new RectangleGeometry(new Rect(new Point(-r, -r), new Point(r, r)));
                m_geometry.Children.Add(rect);
            }

        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                return m_geometry;
            }
        }
    }
}
