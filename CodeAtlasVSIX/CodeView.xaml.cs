using Microsoft.Msagl.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
        DispatcherOperation m_moveState;

        public CodeView()
        {
            InitializeComponent();
            var scene = UIManager.Instance().GetScene();
            scene.View = this;

            ScaleCanvas(0.94, new Point());
        }

        private void testButton_Click(object sender, RoutedEventArgs e)
        {
            //UIManager.Instance().GetScene().OnCloseDB();
            UIManager.Instance().GetScene().OnOpenDB();
        }

        private void canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
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
            //if (m_isViewMoving)
            //{
            //    return;
            //}
            
            m_moveState = Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                var transform = this.canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;

                var centerPnt = new Point(ActualWidth * 0.5, ActualHeight * 0.5);
                var currentPnt = matrix.Transform(center);

                var dist = centerPnt - currentPnt;
                var distLength = dist.Length;
                if (distLength < 1.0)
                {
                    return;
                }

                dist.Normalize();
                var offsetLength = Math.Min(Math.Max(distLength * 0.25, 1.0), distLength);
                m_lastMoveOffset = offsetLength;
                var offset = offsetLength * dist;

                matrix.TranslatePrepend(offset.X, offset.Y);
                transform.Matrix = matrix;
            }, DispatcherPriority.Render);
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

        private void background_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            InvalidateLegend();
            InvalidateScheme();
        }
    }
}
