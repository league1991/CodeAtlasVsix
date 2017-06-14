using Microsoft.Msagl.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Interaction logic for CodeView.xaml.
    /// </summary>
    [ProvideToolboxControl("CodeAtlasVSIX.CodeView", true)]
    public partial class CodeView : Canvas
    {
        public double scaleValue = 1.0;
        public double m_lastMoveOffset = 0.0;

        public CodeView()
        {
            InitializeComponent();
            var scene = UIManager.Instance().GetScene();
            scene.View = this;

            ScaleCanvas(0.9, new Point());
        }

        private void testButton_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, object> data = new Dictionary<string, object>() { { "1", "a" }, { "2", 1 }, { "3", new Dictionary<string, object> { { "1",2} } } };
            JavaScriptSerializer js = new JavaScriptSerializer();
            string jsonData = js.Serialize(data);
            Logger.WriteLine(jsonData);

            var jarr = js.Deserialize<Dictionary<string, object>>(jsonData);

            //UIManager.Instance().GetScene().OnCloseDB();
            UIManager.Instance().GetScene().OnOpenDB();
        }

        private void canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            //var element = this.canvas as UIElement;
            //var position = e.GetPosition(element);
            //var transform = element.RenderTransform as MatrixTransform;
            //var matrix = transform.Matrix;
            //var scale = e.Delta >= 0 ? 1.1 : (1.0 / 1.1); // choose appropriate scaling factor

            //matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);
            //transform.Matrix = matrix;

            var element = this.canvas as UIElement;
            var position = e.GetPosition(element);
            var scale = e.Delta >= 0 ? 1.1 : (1.0 / 1.1); // choose appropriate scaling factor
            ScaleCanvas(scale, position);
        }

        void ScaleCanvas(double scale, Point position)
        {
            var element = this.canvas as UIElement;
            var transform = element.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;

            matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);
            transform.Matrix = matrix;
        }

        public void MoveView(Point center)
        {
            Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                var transform = this.canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;

                var centerPnt = new Point(ActualWidth * 0.5, ActualHeight * 0.5);
                var currentPnt = matrix.Transform(center);

                var offset = (centerPnt - currentPnt) * 0.05;
                m_lastMoveOffset = offset.Length;

                matrix.TranslatePrepend(offset.X, offset.Y);
                transform.Matrix = matrix;
            });
        }

        private void background_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var source = e.OriginalSource;
            if (source == this)
            {
                UIManager.Instance().GetScene().ClearSelection();
            }
        }

        private void background_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.m_autoFocus = false;
        }

        private void background_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            scene.m_autoFocus = true;
        }

        public void InvalidateLegend()
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            legend.BuildLegend();
            scene.ReleaseLock();
            //this.Dispatcher.Invoke((ThreadStart)delegate
            //{
            //    legend.InvalidateVisual();
            //});
        }

        public void InvalidateScheme()
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            scheme.BuildSchemeLegend();
            scene.ReleaseLock();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
        }
        
    }
}
