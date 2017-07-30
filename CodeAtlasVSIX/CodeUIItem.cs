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
using System.Windows.Threading;

namespace CodeAtlasVSIX
{
    public class CodeUIItem : Shape
    {
        public class Variant
        {
            public Variant(string str) { m_string = str; }
            public Variant(int val) { m_int = val; }
            public Variant(double val) { m_double = val; }
            public string m_string;
            public int m_int;
            public double m_double;
        }

        string m_uniqueName = "";
        bool m_isDirty = false;
        string m_name = "";
        string m_longName = "";
        string m_displayName = "";
        string m_kindName = "";
        DoxygenDB.EntKind m_kind = DoxygenDB.EntKind.UNKNOWN;
        int m_lines = 0;
        public int nCallers = 0;
        public int nCallees = 0;
        public string m_bodyCode = "";
        Dictionary<string, DoxygenDB.Variant> m_metric = new Dictionary<string, DoxygenDB.Variant>();

        // UI appearance
        Nullable<Point> dragStart = null;
        GeometryGroup m_geometry = null;
        Point m_targetPos = new Point();
        DateTime m_mouseDownTime = new DateTime();
        bool m_isSelected = false;
        bool m_isHover = false;
        public int m_selectCounter = 0;
        public double m_selectTimeStamp = 0;
        Point m_position = new Point();
        Size m_fontSize = new Size();
        Size m_commentSize = new Size();
        double m_lineHeight = 0.0;
        FormattedText m_displayText = null;
        FormattedText m_commentText = null;
        public bool m_isConnectedToFocusNode = false;
        public double m_accumulateMoveDist = 0.0;
        Dictionary<string, Variant> m_customData;
        Color m_color = new Color();
        bool m_customEdgeMode = false;
        Geometry m_highLightGeometry = new EllipseGeometry();
        bool m_isInvalidating = false;

        public CodeUIItem(string uniqueName, Dictionary<string, object> customData)
        {
            m_uniqueName = uniqueName;
            this.MouseDown += new MouseButtonEventHandler(MouseDownCallback);
            this.MouseUp += new MouseButtonEventHandler(MouseUpCallback);
            this.MouseMove += new MouseEventHandler(MouseMoveCallback);
            this.MouseEnter += new MouseEventHandler(MouseEnterCallback);
            this.MouseLeave += new MouseEventHandler(MouseLeaveCallback);
            this.Cursor = Cursors.Arrow;
            
            var scene = UIManager.Instance().GetScene();

            m_name = (string)customData["name"];
            m_longName = (string)customData["longName"];
            m_kind = (DoxygenDB.EntKind)customData["kind"];
            this.ToolTip = m_longName;
            BuildDisplayName(m_name);
            var comment = scene.GetComment(m_uniqueName);
            UpdateComment(comment);
            m_kindName = (string)customData["kindName"];
            m_metric = (Dictionary<string, DoxygenDB.Variant>)customData["metric"];
            if (m_metric.ContainsKey("CountLine"))
            {
                var metricLine = m_metric["CountLine"].m_int;
                m_lines = metricLine;
            }

            var kindStr = m_kindName.ToLower();

            // custom data
            m_customData = new Dictionary<string, Variant>();
            if (m_kind == DoxygenDB.EntKind.FUNCTION)
            {
                // Find caller and callee count
                int nCaller = (int)customData["nCaller"];
                int nCallee = (int)customData["nCallee"];
                m_customData["nCaller"] = new Variant(nCaller);
                m_customData["nCallee"] = new Variant(nCallee);
                m_customData["callerR"] = new Variant(GetCallerRadius(nCaller));
                m_customData["calleeR"] = new Variant(GetCallerRadius(nCallee));
            }

            m_color = (Color)customData["color"];
            if (m_kind == DoxygenDB.EntKind.FUNCTION || m_kind == DoxygenDB.EntKind.VARIABLE)
            {
                m_customData["hasDef"] = new Variant((int)customData["hasDef"]);
                if (customData.ContainsKey("className"))
                {
                    m_customData["className"] = new Variant((string)customData["className"]);
                }
            }

            SolidColorBrush brush = new SolidColorBrush();
            brush.Color = m_color;
            this.Fill = brush;
            this.Stroke = Brushes.Transparent;
            BuildGeometry();
             
            Canvas.SetZIndex(this, 0);
            this.StrokeThickness = 0.0;
        }

        public Color GetColor()
        {
            return m_color;
        }

        public void GetDefinitionPosition(out string fileName, out int line, out int column)
        {
            fileName = "";
            line = 0;
            column = 0;

            if (m_metric != null)
            {
                var metric = m_metric;
                if (metric.ContainsKey("file"))
                {
                    fileName = metric["file"].m_string;
                    line = metric["line"].m_int;
                    column = metric["column"].m_int;
                }
                if (fileName == "" && metric.ContainsKey("declFile"))
                {
                    fileName = metric["declFile"].m_string;
                    line = metric["declLine"].m_int;
                    column = metric["declColumn"].m_int;
                }
            }
            if (fileName == "")
            {
                var db = DBManager.Instance().GetDB();
                if (db == null)
                {
                    return;
                }
                var entity = db.SearchFromUniqueName(m_uniqueName);
                var refs = db.SearchRef(m_uniqueName, "definein");
                if (refs.Count == 0)
                {
                    refs = db.SearchRef(m_uniqueName, "declarein");
                }
                if (refs.Count == 0)
                {
                    refs = db.SearchRef(m_uniqueName, "callby");
                }
                if (refs.Count == 0)
                {
                    refs = db.SearchRef(m_uniqueName, "useby");
                }
                if (refs.Count == 0)
                {
                    return;
                }
                var refObj = refs[0];
                var fileEnt = refObj.File();
                fileName = fileEnt.Longname();
                line = refObj.Line();
                column = refObj.Column();
            }
        }

        public void AddCustomData(string key, Variant value)
        {
            m_customData[key] = value;
        }

        public Variant GetCustomData(string key)
        {
            if (!m_customData.ContainsKey(key))
            {
                return null;
            }
            return m_customData[key];
        }

        public string GetClassName()
        {
            if (m_kind == DoxygenDB.EntKind.CLASS)
            {
                return m_name;
            }
            if (!m_customData.ContainsKey("className"))
            {
                return "";
            }
            return m_customData["className"].m_string;
        }

        void BuildDisplayName(string name)
        {
            string pattern = @"[A-Z]*[a-z0-9]*_*";
            var nameList = Regex.Matches(
                name,
                pattern,
                RegexOptions.ExplicitCapture
                );

            var partLength = 0;
            m_displayName = "";

            int index = 0;
            int length = 0;
            foreach (Match nextMatch in nameList)
            {
                string newPart = "";
                int beg = index + length;
                if (beg < nextMatch.Index)
                {
                    newPart = name.Substring(beg, nextMatch.Index - beg);
                }
                newPart += nextMatch.Value;

                m_displayName += newPart;
                partLength += newPart.Length;
                if (partLength > 13)
                {
                    m_displayName += "\n";
                    partLength = 0;
                }
                index = nextMatch.Index;
                length = nextMatch.Length;
            }
            m_displayName = m_displayName.Trim();
            int nLine = m_displayName.Count(f => f == '\n') + 1;

            var formattedText = new FormattedText(m_displayName,
                                                    CultureInfo.CurrentUICulture,
                                                    FlowDirection.LeftToRight,
                                                    new Typeface("Arial"),
                                                    12.0,
                                                    Brushes.White);
            m_fontSize = new Size(formattedText.Width, formattedText.Height);
            m_lineHeight = formattedText.LineHeight;
            m_displayText = formattedText;
        }

        public void UpdateComment(string comment)
        {
            if (comment == "")
            {
                m_commentSize = new Size();
            }

            var formattedText = new FormattedText(comment,
                                                    CultureInfo.CurrentUICulture,
                                                    FlowDirection.LeftToRight,
                                                    new Typeface("tahoma"),
                                                    12.0,
                                                    Brushes.Black);
            formattedText.Trimming = TextTrimming.None;
            formattedText.MaxTextWidth = 100;
            m_commentText = formattedText;
            m_commentSize = new Size(100, (formattedText.Height));
            IsDirty = true;
        }

        public bool IsFunction()
        {
            return m_kind == DoxygenDB.EntKind.FUNCTION;
        }

        public bool IsVariable()
        {
            return m_kind == DoxygenDB.EntKind.VARIABLE;
        }

        public bool IsClassOrStruct()
        {
            return m_kind == DoxygenDB.EntKind.CLASS ||
                m_kind == DoxygenDB.EntKind.STRUCT;
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

        public static Color NameToColor(string name)
        {
            uint hashVal = (uint)name.GetHashCode();
            var h = ((hashVal) & 0xffff) / 65535.0;
            var s = ((hashVal >> 16) & 0xff) / 255.0;
            var l = ((hashVal >> 24) & 0xff) / 255.0;
            return HSLToRGB(h, 0.35+s*0.3, 0.4+l*0.15);
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

        public bool IsInvalidating()
        {
            return m_isInvalidating;
        }

        public void Invalidate()
        {
            if (m_isDirty && !m_isInvalidating)
            {
                m_isInvalidating = true;
                this.Dispatcher.BeginInvoke((ThreadStart)delegate
                {
                    this._Invalidate();
                    m_isInvalidating = false;
                });
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
                    if (m_isSelected)
                    {
                        this.Stroke = new SolidColorBrush(Color.FromRgb(255, 157, 38));
                    }
                    else
                    {
                        this.Stroke = Brushes.Transparent;
                    }
                    UIManager.Instance().GetScene().OnSelectItems();
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
                Canvas.SetLeft(this, value.X);
                Canvas.SetTop(this, value.Y);
                m_position = value;
                IsDirty = true;
            }
            get
            {
                return m_position;
            }
        }

        public double GetRadius()
        {
            var r = GetBodyRadius();
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
            if (m_kind == DoxygenDB.EntKind.FILE)
            {
                r = Math.Pow((double)(m_lines + 1), 0.2) * 2.5;
            }
            else if (m_kind != DoxygenDB.EntKind.VARIABLE)
            {
                r = Math.Pow((double)(m_lines + 1), 0.3) * 2.5;
            }
            return r;
        }

        public double GetHeight()
        {
            double r = GetRadius();
            double h = Math.Max(m_fontSize.Height + m_commentSize.Height, r);
            h += r;
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
            var pos = Pos;
            return new Point(pos.X + GetRightSlotOffset(), pos.Y);
        }


        public double GetRightSlotOffset()
        {
            var l = GetBodyRadius();
            if (IsFunction())
            {
                l += m_customData["calleeR"].m_double;
            }
            return l;
        }

        public void SetTargetPos(Point point)
        {
            m_targetPos = point;
        }

        public Vector DispToTarget()
        {
            return m_targetPos - Pos;
        }

        public double MoveToTarget(double ratio)
        {
            Vector offset = m_targetPos - Pos;
            var offsetLength = offset.Length;
            offset.Normalize();
            if (offsetLength < 0.5)
            {
                return 0.0;
            }

            var speed = Math.Min(offsetLength * ratio, 40);
            var minSpeed = 0.5;
            var moveDist = Math.Min(Math.Max(minSpeed, speed), offsetLength);
            Pos = Pos + offset * moveDist;
            return moveDist;

            //if (m_accumulateMoveDist < 1)
            //{
            //    m_accumulateMoveDist += offsetLength * ratio;
            //    return 0.0;
            //}
            //else
            //{
            //    Pos = Pos + offset * Math.Min(m_accumulateMoveDist, offsetLength);
            //    m_accumulateMoveDist = 0.0;
            //    return offsetLength * ratio;
            //}
        }

        public DoxygenDB.EntKind GetKind()
        {
            return m_kind;
        }

        public string GetUniqueName()
        {
            return m_uniqueName;
        }

        public string GetName()
        {
            return m_name;
        }

        public string GetLongName()
        {
            return m_longName;
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
            if (args.LeftButton == MouseButtonState.Pressed)
            {
                m_mouseDownTime = newDownTime;
            }
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
            scene.ClearSelection();
            scene.SelectCodeItem(this.m_uniqueName);
            _BuildContextMenu();
            CaptureMouse();
            dragStart = args.GetPosition(this);
        }

        public void SetCustomEdgeSourceMode(bool isCustomSource)
        {
            if (m_customEdgeMode != isCustomSource)
            {
                m_customEdgeMode = isCustomSource;
                IsDirty = true;
            }
        }

        public bool GetCustomEdgeSourceMode()
        {
            return m_customEdgeMode;
        }

        public void OnAddCustomEdge(object sender, ExecutedRoutedEventArgs e)
        {
            CaptureMouse();
            SetCustomEdgeSourceMode(true);
            var scene = UIManager.Instance().GetScene();
            scene.m_customEdgeSource = m_uniqueName;
        }

        void _AddContextMenuItem(ContextMenu context, string header, ExecutedRoutedEventHandler handler)
        {
            MenuItem menuItem = new MenuItem();
            menuItem.Header = header;
            menuItem.Click += delegate { handler(null, null); };
            context.Items.Add(menuItem);
        }

        void _BuildContextMenu()
        {
            var mainUI = UIManager.Instance().GetMainUI();
            ContextMenu context = new ContextMenu();
            if (m_kind == DoxygenDB.EntKind.FUNCTION)
            {
                _AddContextMenuItem(context, "Find Callers", mainUI.OnFindCallers);
                _AddContextMenuItem(context, "Find Callees", mainUI.OnFindCallees);
                _AddContextMenuItem(context, "Find Overrides", mainUI.OnFindOverrides);
            }
            else if(m_kind == DoxygenDB.EntKind.CLASS)
            {
                _AddContextMenuItem(context, "Find Bases", mainUI.OnFindBases);
            }
            _AddContextMenuItem(context, "Find Usages", mainUI.OnFindUses);
            _AddContextMenuItem(context, "Delete", mainUI.OnDelectSelectedItems);
            _AddContextMenuItem(context, "Delete and Ignore", mainUI.OnDeleteSelectedItemsAndAddToStop);
            _AddContextMenuItem(context, "Delete Nearby Items", mainUI.OnDeleteNearbyItems);
            _AddContextMenuItem(context, "Add Similar Items", mainUI.OnAddSimilarCodeItem);
            //_AddContextMenuItem(context, "Add Custom Edge", this.OnAddCustomEdge);
            _AddContextMenuItem(context, "Mark As Custom Edge Source", mainUI.OnBeginCustomEdge);
            _AddContextMenuItem(context, "Connect Custom Edge From Source", mainUI.OnEndCustomEdge);
            this.ContextMenu = context;
        }

        void MouseDoubleClickCallback(object sender, MouseEventArgs args)
        {
            var scene = UIManager.Instance().GetScene();
            scene.ClearSelection();
            scene.SelectCodeItem(this.m_uniqueName);
            UIManager.Instance().GetScene().ShowInEditor();
        }

        void MouseMoveCallback(object sender, MouseEventArgs args)
        {
            if (dragStart != null && args.LeftButton == MouseButtonState.Pressed)
            {
                var canvas = GetCanvas();
                var p2 = args.GetPosition(canvas);
                Pos = new Point(p2.X - dragStart.Value.X, p2.Y - dragStart.Value.Y);
                SetTargetPos(Pos);
            }
            if (m_customEdgeMode)
            {
                IsDirty = true;
            }
        }

        void MouseUpCallback(object sender, MouseEventArgs e)
        {
            dragStart = null;
            ReleaseMouseCapture();

            //var scene = UIManager.Instance().GetScene();
            //if (m_customEdgeMode)
            //{
            //    // create custom edge
            //    var uiItem = Mouse.DirectlyOver as CodeUIItem;
            //    if (uiItem != null && uiItem != this)
            //    {
            //        scene.DoAddCustomEdge(this.m_uniqueName, uiItem.GetUniqueName());
            //    }
            //    SetCustomEdgeSourceMode(false);
            //}
        }

        void MouseEnterCallback(object sender, MouseEventArgs e)
        {
            if (IsSelected)
            {

            }
            else
            {
                this.Stroke = new SolidColorBrush(Color.FromRgb(255, 157, 38));
                //InvalidateVisual();
            }
            m_isHover = true;
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
            m_isHover = false;
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

                var r0 = r;
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

                m_highLightGeometry = new EllipseGeometry(new Point(0.0, 0.0), r, r);
                if (m_lines == 0 || (m_customData.ContainsKey("hasDef") && m_customData["hasDef"].m_int == 0))
                {
                    var innerCircle = new EllipseGeometry(new Point(0.0, 0.0), 1.5,1.5);
                    m_highLightGeometry = Geometry.Combine(m_highLightGeometry, innerCircle, GeometryCombineMode.Exclude, null);
                }
                m_geometry.Children.Add(m_highLightGeometry);
            }
            else if (m_kind == DoxygenDB.EntKind.VARIABLE)
            {
                var figure = new PathFigure();
                figure.StartPoint = new Point(-r, 0.0);
                figure.Segments.Add(new LineSegment(new Point(0, r * 0.5), true));
                figure.Segments.Add(new LineSegment(new Point(0, -r * 0.5), true));
                figure.IsClosed = true;
                figure.IsFilled = true;
                var pathGeo = new PathGeometry();
                pathGeo.Figures.Add(figure);
                m_geometry.Children.Add(pathGeo);
                m_highLightGeometry = pathGeo;
            }
            else if(m_kind == DoxygenDB.EntKind.CLASS)
            {
                var rect = new RectangleGeometry(new Rect(new Point(-r, -r), new Point(r, r)));
                m_geometry.Children.Add(rect);
                m_highLightGeometry = rect;
            }
            else if (m_kind == DoxygenDB.EntKind.FILE)
            {
                var figure = new PathFigure();
                figure.StartPoint = new Point(r, 0.0);
                figure.Segments.Add(new LineSegment(new Point(r * 0.5, r * 0.866), true));
                figure.Segments.Add(new LineSegment(new Point(r * -0.5, r * 0.866), true));
                figure.Segments.Add(new LineSegment(new Point(r * -1, 0.0), true));
                figure.Segments.Add(new LineSegment(new Point(r * -0.5, r * -0.866), true));
                figure.Segments.Add(new LineSegment(new Point(r * 0.5, r * -0.866), true));
                figure.IsClosed = true;
                figure.IsFilled = true;
                var pathGeo = new PathGeometry();
                pathGeo.Figures.Add(figure);
                m_geometry.Children.Add(pathGeo);
                m_highLightGeometry = pathGeo;
            }
            else
            {
                float w = 3.0f;
                var rect = new RectangleGeometry(new Rect(new Point(-w, -w), new Point(w, w)));
                var trans = new MatrixTransform();
                var mat = new Matrix();
                mat.RotateAtPrepend(45.0,0.0,0.0);
                trans.Matrix = mat;
                rect.Transform = trans;
                m_geometry.Children.Add(rect);
                m_highLightGeometry = rect;
                this.Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
            }
        }

        protected override Geometry DefiningGeometry
        {
            get
            {
                return m_geometry;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // Draw highlight first
            if (m_customEdgeMode)
            {
                var circumference = GetRadius() * 2.0 * Math.PI;
                var edgeStroke = new SolidColorBrush(Color.FromArgb(130, 255, 157, 38));
                var edgePen = new Pen(edgeStroke, 30.0);
                drawingContext.DrawGeometry(edgeStroke, edgePen, m_highLightGeometry);
            }
            if (m_highLightGeometry != null && (m_isSelected || m_isHover))
            {
                var edgeStroke = new SolidColorBrush(Color.FromRgb(255, 157, 38));
                drawingContext.DrawGeometry(edgeStroke, new Pen(edgeStroke, 10.0), m_highLightGeometry);
            }

            base.OnRender(drawingContext);
            double baseX = 4;
            double baseY = 0;
            if (this.m_kind == DoxygenDB.EntKind.VARIABLE)
            {
                baseY -= 8;
            }
            if (m_displayText != null)
            {
                m_displayText.SetForegroundBrush(new SolidColorBrush(Color.FromRgb(10,10,10)));
                drawingContext.DrawText(m_displayText, new Point(baseX+1.0, baseY+1.0));
                m_displayText.SetForegroundBrush(new SolidColorBrush(Color.FromRgb(255, 239, 183)));
                drawingContext.DrawText(m_displayText, new Point(baseX, baseY));
            }
            if (m_commentText != null)
            {
                baseY += m_displayText.Height;
                m_commentText.SetForegroundBrush(new SolidColorBrush(Color.FromRgb(197,236,79)));
                drawingContext.DrawText(m_commentText, new Point(baseX, baseY));
            }
            //if (m_customEdgeMode)
            //{
            //    var p0 = new Point(GetRightSlotOffset(), 0);
            //    var p3 = Mouse.GetPosition(this);
            //    var p1 = new Point(p0.X * 0.5 + p3.X * 0.5, p0.Y);
            //    var p2 = new Point(p0.X * 0.5 + p3.X * 0.5, p3.Y);

            //    var segment = new BezierSegment(p1, p2, p3, true);
            //    var figure = new PathFigure();
            //    figure.StartPoint = p0;
            //    figure.Segments.Add(segment);
            //    figure.IsClosed = false;

            //    var pathGeo = new PathGeometry();
            //    pathGeo.Figures.Add(figure);

            //    var pen = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 2);
            //    drawingContext.DrawGeometry(Brushes.Transparent, pen, pathGeo);
            //}
        }
    }
}
