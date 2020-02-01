using Microsoft.Msagl.Drawing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Interaction logic for CodeView.xaml.
    /// </summary>
    [ProvideToolboxControl("CodeAtlasVSIX.CodeView", true)]
    public partial class CodeView : Grid
    {
        public double scaleValue = 1.0;
        public double m_lastMoveOffset = 0.0;
        DispatcherOperation m_moveState;
        public bool m_isMouseDown = false;
        Point m_backgroundMovePos = new Point();
        DateTime m_mouseMoveTime = new DateTime();
        public bool m_isMouseInView = true;
        public bool m_isAutoFocus = true;

        // Frame Selection
        Point m_rectBeginPos = new Point();
        bool m_isFrameSelection = false;

        public CodeView()
        {
            InitializeComponent();
            var scene = UIManager.Instance().GetScene();
            scene.View = this;

            ScaleCanvas(1.0, new Point());
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

        public void SetAutoFocus(bool isAutoFocus)
        {
            m_isAutoFocus = isAutoFocus;
        }

        public void MoveView(Point center)
        {
            double speedFactor = 0.15f;
            if (m_isMouseInView)
            {
                speedFactor = (DateTime.Now - m_mouseMoveTime).TotalSeconds > 7 ? 0.15f : 0.0;
            }
            else
            {
                speedFactor = (DateTime.Now - m_mouseMoveTime).TotalSeconds > 1 ? 0.15f : 0.0;
            }
            if (speedFactor == 0.0 || !m_isAutoFocus)
            {
                return;
            }

            m_moveState = Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                var transform = this.canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;

                var centerPnt = new Point(ActualWidth * 0.5, ActualHeight * 0.5);
                var currentPnt = matrix.Transform(center);

                float padding = 0.2f;
                if (currentPnt.X > ActualWidth * padding && currentPnt.X < ActualWidth * (1- padding) &&
                    currentPnt.Y > ActualHeight * padding && currentPnt.Y < ActualHeight * (1 - padding))
                {
                    return;
                }
                var dist = centerPnt - currentPnt;
                var distLength = dist.Length / transform.Matrix.M11;
                if (distLength < 2.0)
                {
                    return;
                }

                dist.Normalize();
                var speedLimit = distLength * 0.25;
                var minSpeed = 1.0;
                var offsetLength = Math.Min(Math.Max(minSpeed, speedLimit), distLength) * speedFactor;
                var offset = offsetLength * dist;
                m_lastMoveOffset = offsetLength;

                matrix.TranslatePrepend(offset.X, offset.Y);
                transform.Matrix = matrix;
            });
        }

        private void background_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            m_isMouseDown = true;
            m_backgroundMovePos = e.GetPosition(background);
            var source = e.OriginalSource;
            if (source == this && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                if (!(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    UIManager.Instance().GetScene().SelectNothing();
                }

                var canvasTrans = canvas.RenderTransform as MatrixTransform;
                m_isFrameSelection = true;
                m_rectBeginPos = e.GetPosition(canvas);
                selectionRect.Width = 10;
                selectionRect.Height = 10;
                selectionRect.StrokeThickness = 1 / canvasTrans.Matrix.M11;
                Canvas.SetLeft(selectionRect, m_rectBeginPos.X);
                Canvas.SetTop(selectionRect, m_rectBeginPos.Y);
                selectionRect.Visibility = Visibility.Visible;
                selectionRect.CaptureMouse();
            }
        }

        private void background_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var source = e.OriginalSource;
            m_isMouseDown = false;
            ReleaseMouseCapture();
            if (m_isFrameSelection)
            {
                PerformFrameSelection();
                m_isFrameSelection = false;
                selectionRect.ReleaseMouseCapture();
                selectionRect.Visibility = Visibility.Collapsed;
            }
        }

        void PerformFrameSelection()
        {
            bool isClean = !(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl));
            var scene = UIManager.Instance().GetScene();
            var itemDict = scene.GetItemDict();
            var itemKeyList = new List<string>();
            var rectGeometry = selectionRect.RenderedGeometry.Clone();
            var rectTrans = selectionRect.TransformToVisual(canvas) as MatrixTransform;
            rectGeometry.Transform = rectTrans;
            foreach (var itemPair in itemDict)
            {
                var itemGeo = itemPair.Value.RenderedGeometry.Clone();
                var itemTrans = itemPair.Value.TransformToVisual(canvas) as MatrixTransform;
                itemGeo.Transform = itemTrans;
                var isect = rectGeometry.FillContainsWithDetail(itemGeo);
                if (isect != IntersectionDetail.Empty && isect != IntersectionDetail.NotCalculated)
                {
                    itemKeyList.Add(itemPair.Key);
                }
            }

            var edgeDict = scene.GetEdgeDict();
            var edgeKeyList = new List<Tuple<string, string>>();
            foreach (var itemPair in edgeDict)
            {
                var itemGeo = itemPair.Value.RenderedGeometry.Clone();
                var itemTrans = itemPair.Value.TransformToVisual(canvas) as MatrixTransform;
                itemGeo.Transform = itemTrans;
                var isect = rectGeometry.FillContainsWithDetail(itemGeo);
                if (isect != IntersectionDetail.Empty && isect != IntersectionDetail.NotCalculated)
                {
                    edgeKeyList.Add(itemPair.Key);
                }
            }
            scene.SelectItemsAndEdges(itemKeyList, edgeKeyList, isClean);
        }

        private void background_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var source = e.OriginalSource;
            //if (source == this || true)
            {
                var point = e.GetPosition(background);
                if (!Point.Equals(point, m_backgroundMovePos))
                {
                    m_mouseMoveTime = DateTime.Now;
                }
                
                var scene = UIManager.Instance().GetScene();
                var selectedItems = scene.SelectedItems();
                if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    CaptureMouse();
                    var element = this.canvas as UIElement;
                    var transform = this.canvas.RenderTransform as MatrixTransform;
                    var matrix = transform.Matrix;
                    var offset = (point - m_backgroundMovePos) / matrix.M11;
                    matrix.TranslatePrepend(offset.X, offset.Y);
                    transform.Matrix = matrix;
                }
                else
                {
                    m_isMouseDown = false;
                }
                m_backgroundMovePos = point;
            }
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && m_isFrameSelection)
            {
                var newPoint = e.GetPosition(canvas);
                var delta = newPoint - m_rectBeginPos;
                var rectWidth = Math.Abs(delta.X);
                var rectHeight = Math.Abs(delta.Y);
                var topPoint = delta.Y >= 0 ? m_rectBeginPos.Y : m_rectBeginPos.Y + delta.Y;
                var leftPoint = delta.X >= 0 ? m_rectBeginPos.X : m_rectBeginPos.X + delta.X;
                selectionRect.Width = rectWidth;
                selectionRect.Height = rectHeight;
                Canvas.SetLeft(selectionRect, leftPoint);
                Canvas.SetTop(selectionRect, topPoint);
            }
        }

        private void background_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            m_isMouseInView = true;
        }

        private void background_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var scene = UIManager.Instance().GetScene();
            m_isMouseInView= false;
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

        public void InvalidateFileList()
        {
            var scene = UIManager.Instance().GetScene();
            scene.AcquireLock();
            fileList.BuildFileListLegend();
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

        public void ExportToPng(string filePath)
        {
            if (filePath == null) return;

            var surface = this.background;

            Size size = new Size(this.ActualWidth, this.ActualHeight);

            RenderTargetBitmap renderBitmap =
            new RenderTargetBitmap(
            (int)size.Width*300/96,
            (int)size.Height*300/96,
            300d,
            300d,
            PixelFormats.Pbgra32);
            renderBitmap.Render(surface);

            using (FileStream outStream = new FileStream(filePath, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                encoder.Save(outStream);
            }
        }
    }
}
