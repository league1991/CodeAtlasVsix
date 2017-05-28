﻿using Microsoft.Msagl.Drawing;
using System.Globalization;
using System.Threading;
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
        public CodeView()
        {
            InitializeComponent();
            var scene = UIManager.Instance().GetScene();
            scene.View = this;
        }

        private void testButton_Click(object sender, RoutedEventArgs e)
        {
            // this.canvas.Children.Add(new CodeUIItem());
            var scene = UIManager.Instance().GetScene();

            var rand = new System.Random();
            var srcId = rand.Next().ToString();
            var tarId = rand.Next().ToString();

            scene.AddCodeItem(srcId);
            scene.AddCodeItem(tarId);
            scene.AddCodeEdgeItem(srcId, tarId);

            //Graph graph = new Graph();
            //graph.AddEdge("47", "58");
            //graph.AddEdge("70", "71");


            //var subgraph = new Subgraph("subgraph1");
            //graph.RootSubgraph.AddSubgraph(subgraph);
            //subgraph.AddNode(graph.FindNode("47"));
            //subgraph.AddNode(graph.FindNode("58"));

            //var subgraph2 = new Subgraph("subgraph2");
            //subgraph2.Attr.Color = Microsoft.Msagl.Drawing.Color.Black;
            //subgraph2.Attr.FillColor = Microsoft.Msagl.Drawing.Color.Yellow;
            //subgraph2.AddNode(graph.FindNode("70"));
            //subgraph2.AddNode(graph.FindNode("71"));
            //subgraph.AddSubgraph(subgraph2);
            //graph.AddEdge("58", subgraph2.Id);
            //graph.Attr.LayerDirection = LayerDirection.LR;
            //graph.CreateGeometryGraph();
            //foreach(var msaglNode in graph.GeometryGraph.Nodes)
            //{
            //    var node = (Microsoft.Msagl.Drawing.Node)msaglNode.UserData;
            //    msaglNode.BoundaryCurve = NodeBoundaryCurves.GetNodeBoundaryCurve(node, 5, 5);
            //}
            //Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(graph.GeometryGraph, graph.LayoutAlgorithmSettings, new Microsoft.Msagl.Core.CancelToken());
        }

        private void canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            //Point position = e.GetPosition(this.canvas);
            //scaleValue += e.Delta * 0.005;
            //ScaleTransform scale = new ScaleTransform(scaleValue, scaleValue, position.X, position.Y);
            //this.canvas.RenderTransform = scale;
            //this.canvas.UpdateLayout();

            var element = this.canvas as UIElement;
            var position = e.GetPosition(element);
            var transform = element.RenderTransform as MatrixTransform;
            var matrix = transform.Matrix;
            var scale = e.Delta >= 0 ? 1.1 : (1.0 / 1.1); // choose appropriate scaling factor

            matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);
            transform.Matrix = matrix;
        }

        public void MoveView(Point center)
        {
            Dispatcher.Invoke((ThreadStart)delegate
            {
                var transform = this.canvas.RenderTransform as MatrixTransform;
                var matrix = transform.Matrix;

                var centerPnt = new Point(ActualWidth * 0.5, ActualHeight * 0.5);
                var currentPnt = matrix.Transform(center);

                var offset = (centerPnt - currentPnt) * 0.05;

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

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
        }
    }
}
