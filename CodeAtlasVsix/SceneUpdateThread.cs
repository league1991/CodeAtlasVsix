using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CodeAtlasVSIX
{
    class SceneUpdateThread
    {
        class ItemData
        {

        }
        int m_sleepTime = 30;
        Thread m_thread = null;
        bool m_isActive = true;
        Dictionary<string, ItemData> m_itemSet = new Dictionary<string, ItemData>();
        int m_edgeNum = 0;

        public SceneUpdateThread(CodeScene scene)
        {
            m_thread = new Thread(new ThreadStart(Run));
        }

        public void Start()
        {
            m_thread.Start();
        }

        void Run()
        {
            var scene = UIManager.Instance().GetScene();
            while(true)
            {
                if(m_isActive)
                {
                    scene.AcquireLock();
                    var itemDict = scene.GetItemDict();
                    if(scene.m_isLayoutDirty)
                    {
                        UpdateLayeredLayoutWithComp();

                        // update internal dict
                        m_itemSet.Clear();
                        foreach(var item in itemDict)
                        {
                            m_itemSet.Add(item.Key, new ItemData());
                        }
                        scene.m_isLayoutDirty = false;
                    }
                    scene.ReleaseLock();

                    MoveItems();
                    InvalidateScene();
                    // System.Console.Write("running\n");
                }
                Thread.Sleep(m_sleepTime);
            }
        }

        void UpdateLayeredLayoutWithComp()
        {
            var scene = UIManager.Instance().GetScene();

            Graph graph = new Graph();

            var itemDict = scene.GetItemDict();
            var edgeDict = scene.GetEdgeDict();
            foreach(var item in itemDict)
            {
                var node = graph.AddNode(item.Key);
            }
            foreach(var edge in edgeDict)
            {
                var key = edge.Key;
                graph.AddEdge(key.Item1, key.Item2);
            }
            graph.Attr.LayerDirection = LayerDirection.LR;
            graph.CreateGeometryGraph();
            var layerSetting = graph.LayoutAlgorithmSettings as SugiyamaLayoutSettings;
            if (layerSetting != null)
            {
                layerSetting.LayerSeparation = 80;
            }
            foreach (var msaglNode in graph.GeometryGraph.Nodes)
            {
                var node = (Microsoft.Msagl.Drawing.Node)msaglNode.UserData;
                var sceneNode = itemDict[node.Id];
                double radius = sceneNode.GetRadius();
                double width = sceneNode.GetWidth();
                msaglNode.BoundaryCurve = NodeBoundaryCurves.GetNodeBoundaryCurve(node, width, radius * 2.0);
            }
            Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(graph.GeometryGraph, graph.LayoutAlgorithmSettings, new Microsoft.Msagl.Core.CancelToken());

            foreach (var msaglNode in graph.GeometryGraph.Nodes)
            {
                var node = (Microsoft.Msagl.Drawing.Node)msaglNode.UserData;
                var sceneNode = itemDict[node.Id];
                var pos = node.Pos;
                sceneNode.SetTargetPos(new Point(pos.X, pos.Y));
            }
        }

        void MoveItems()
        {
            var scene = UIManager.Instance().GetScene();
            scene.MoveItems();
            if(scene.View != null)
            {
                Point centerPnt;
                bool res = scene.GetSelectedCenter(out centerPnt);
                if (res)
                {
                    scene.View.MoveView(centerPnt);
                }
            }
        }

        void UpdateCallOrder()
        {

        }

        void InvalidateScene()
        {
            UIManager.Instance().GetScene().Invalidate();
        }

    }
}
