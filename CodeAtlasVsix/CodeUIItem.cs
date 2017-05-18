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
        class Variant
        {
            public Variant(string str) { m_string = str; }
            public Variant(int val) { m_int = val; }
            public Variant(double val) { m_double = val; }
            public string m_string;
            public int m_int;
            public double m_double;
        }

        public float m_radius = 10.0f;
        public int nCallers = 0;
        public int nCallees = 0;

        Nullable<Point> dragStart = null;
        GeometryGroup geometry = null;
        string m_uniqueName = "";
        bool m_isDirty = false;
        Point m_targetPos = new Point();
        DateTime m_mouseDownTime = new DateTime();
        bool m_isSelected = false;
        bool m_isHover = false;
        Point m_position = new Point();
        int m_selectCounter = 0;
        double m_selectTimeStamp = 0;

        string m_name = "";
        string m_displayName = "";
        int m_lines = 0;
        string m_kindName = "";
        DoxygenDB.EntKind m_kind = DoxygenDB.EntKind.UNKNOWN;
        static FontFamily s_titleFont = new FontFamily("tahoma");
        Size m_fontSize = new Size();
        Size m_commentSize = new Size();
        int m_lineHeight = 0;
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
                        var declareEnt = defineList[0];
                        if (declareEnt.KindName().ToLower().Contains("class") ||
                            declareEnt.KindName().ToLower().Contains("struct"))
                        {
                            name = declareEnt.Name();
                            m_customData.Add("className", new Variant(name));
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

        }

        void BuildCommentSize(string comment)
        {

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
            uint hashVal = (uint)name.GetHashCode() & 0xffffffff;
            var h = (hashVal & 0xff) / 255.0;
            var s = ((hashVal >> 8) & 0xff) / 255.0;
            var l = ((hashVal >> 16) & 0xff) / 255.0;
            return HSLToRGB(h, s, l);
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
