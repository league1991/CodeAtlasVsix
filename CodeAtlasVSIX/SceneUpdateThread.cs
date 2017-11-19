using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.Layout.Layered;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CodeAtlasVSIX
{
    class SceneUpdateThread
    {
        class ItemData
        {
        }

        int m_forceSleepTime = -1;
        int m_sleepTime = 30;
        Thread m_thread = null;
        bool m_isActive = true;
        int m_selectTimeStamp = 0;
        int m_schemeTimeStamp = 0;
        Dictionary<string, ItemData> m_itemSet = new Dictionary<string, ItemData>();
        //int m_edgeNum = 0;
        bool m_abort = false;
        int m_timeStamp = 0;

        public SceneUpdateThread(CodeScene scene)
        {
            m_thread = new Thread(new ThreadStart(Run));
            m_thread.Name = "Scene Update Thread";
            m_thread.Priority = ThreadPriority.Lowest;
        }

        public void Start()
        {
            m_thread.Start();
        }

        public void Terminate()
        {
            m_abort = true;
        }

        public void SetActive(bool active)
        {
            m_isActive = active;
        }

        void BeginTimeStamp()
        {
            m_timeStamp = System.Environment.TickCount;
        }

        int EndTimeStamp(string info)
        {
            var now = System.Environment.TickCount;
            var ms = (now - m_timeStamp);
            m_timeStamp = now;
            return ms;
            //var scene = UIManager.Instance().GetScene();
            //scene.Dispatcher.BeginInvoke((ThreadStart)delegate
            //{
            //    Logger.Debug(info + ":" + ms.ToString());
            //});
            //return ms;
        }

        public void SetForceSleepTime(int t)
        {
            m_forceSleepTime = t;
        }

        public void ClearForceSleepTime()
        {
            m_forceSleepTime = -1;
        }

        void Run()
        {
            while (true)
            {
                var scene = UIManager.Instance().GetScene();
                var mainUI = UIManager.Instance().GetMainUI();
                if (m_abort)
                {
                    break;
                }

                var beginTime = DateTime.Now;
                if (m_isActive)
                {
                    EndTimeStamp("++++++++++++++++++++ begin thread ++++++++++++++++++++");

                    m_timeStamp = System.Environment.TickCount;
                    int layoutTime = 0;
                    if (scene.m_isLayoutDirty)
                    {
                        scene.AcquireLock();
                        BeginTimeStamp();
                        UpdateLayeredLayoutWithComp();
                        scene.m_isLayoutDirty = false;
                        layoutTime = EndTimeStamp("Layout");
                        scene.ReleaseLock();
                    }

                    Thread.Sleep(m_sleepTime);
                    scene.AcquireLock();
                    BeginTimeStamp();
                    MoveItems();
                    int moveTime = EndTimeStamp("Move Items");
                    scene.ReleaseLock();
                    
                    scene.AcquireLock();
                    BeginTimeStamp();
                    UpdateCallOrder();
                    int callOrderTime = EndTimeStamp("Call Order");
                    scene.ReleaseLock();
                    //Thread.Sleep(m_sleepTime / 2 + callOrderTime);

                    scene.AcquireLock();
                    if (m_selectTimeStamp != scene.m_selectTimeStamp || m_schemeTimeStamp != scene.m_schemeTimeStamp)
                    {
                        BeginTimeStamp();
                        scene.UpdateCurrentValidScheme();
                        EndTimeStamp("Scheme");
                        m_schemeTimeStamp = scene.m_schemeTimeStamp;
                    }
                    BeginTimeStamp();
                    scene.UpdateCandidateEdge();
                    EndTimeStamp("Candidate Edge");

                    if (m_selectTimeStamp != scene.m_selectTimeStamp)
                    {
                        BeginTimeStamp();
                        UpdateLegend();
                        EndTimeStamp("Legend");
                        m_selectTimeStamp = scene.m_selectTimeStamp;
                    }

                    EndTimeStamp("---------------------- end thread ------------------");
                    scene.ReleaseLock();

                    scene.ClearInvalidate();

                    mainUI.Dispatcher.BeginInvoke((ThreadStart)delegate
                    {
                        mainUI.CheckFindSymbolWindow(null, null);
                    }, DispatcherPriority.Loaded);
                }
                else
                {
                    Thread.Sleep(m_sleepTime);
                }

                var moveDistance = scene.m_itemMoveDistance;
                if (scene.View != null)
                {
                    moveDistance += scene.View.m_lastMoveOffset;
                }
                
                if (m_forceSleepTime > 0)
                {
                    m_sleepTime = m_forceSleepTime;
                }
                else
                {
                    m_sleepTime = (moveDistance > 0.1) ? 30 : 500;
                }
                //m_sleepTime = 2000;
            }
        }

        void UpdateLayeredLayoutWithComp()
        {
            var scene = UIManager.Instance().GetScene();

            Graph graph = new Graph();

            var itemDict = scene.GetItemDict();
            var edgeDict = scene.GetEdgeDict();
            foreach (var item in itemDict)
            {
                var node = graph.AddNode(item.Key);
            }
            foreach (var edge in edgeDict)
            {
                var key = edge.Key;
                graph.AddEdge(key.Item1, key.Item2);
            }
            graph.Attr.LayerDirection = LayerDirection.LR;
            graph.CreateGeometryGraph();
            var layerSetting = graph.LayoutAlgorithmSettings as SugiyamaLayoutSettings;
            if (layerSetting != null)
            {
                layerSetting.LayerSeparation = 120;
                layerSetting.NodeSeparation = 4;
            }
            foreach (var msaglNode in graph.GeometryGraph.Nodes)
            {
                var node = (Microsoft.Msagl.Drawing.Node)msaglNode.UserData;
                var sceneNode = itemDict[node.Id];
                double radius = sceneNode.GetRadius();
                double width = sceneNode.GetWidth();
                double height = sceneNode.GetHeight();
                msaglNode.BoundaryCurve = NodeBoundaryCurves.GetNodeBoundaryCurve(node, width, height);
            }
            Microsoft.Msagl.Miscellaneous.LayoutHelpers.CalculateLayout(graph.GeometryGraph, graph.LayoutAlgorithmSettings, new Microsoft.Msagl.Core.CancelToken());

            foreach (var msaglNode in graph.GeometryGraph.Nodes)
            {
                var node = (Microsoft.Msagl.Drawing.Node)msaglNode.UserData;
                var nodeBegin = node.Pos.Y - node.Height * 0.5;
                var sceneNode = itemDict[node.Id];
                double radius = sceneNode.GetRadius();
                double width = sceneNode.GetWidth();
                double height = sceneNode.GetHeight();
                var pos = node.Pos;
                sceneNode.SetTargetPos(new Point(pos.X, nodeBegin + radius));
            }
        }

        void UpdateLegend()
        {
            var scene = UIManager.Instance().GetScene();
            if (scene.View != null)
            {
                scene.View.InvalidateLegend();
                scene.View.InvalidateScheme();
            }
        }

        void MoveItems()
        {
            var scene = UIManager.Instance().GetScene();
            scene.MoveItems();
            if (scene.View != null)
            {
                Point centerPnt;
                bool res = scene.GetSelectedCenter(out centerPnt);
                if (res && scene.IsAutoFocus())
                {
                    scene.View.MoveView(centerPnt);
                }
            }
        }

        void UpdateCallOrder()
        {
            var scene = UIManager.Instance().GetScene();
            var edgeDict = scene.GetEdgeDict();
            var itemDict = scene.GetItemDict();
            foreach (var item in edgeDict)
            {
                var key = item.Key;
                var edge = item.Value;
                edge.m_isConnectedToFocusNode = false;
            }

            foreach (var item in itemDict)
            {
                item.Value.m_isConnectedToFocusNode = false;
            }

            var items = scene.SelectedItems();
            if (items.Count == 0)
            {
                return;
            }

            var selectedItem = items[0];
            var orderEdge = new Dictionary<Tuple<string, string>, OrderData>();
            UpdateCallOrderByItem(selectedItem, ref orderEdge);

            var selectedUIItem = selectedItem as CodeUIItem;
            if (selectedUIItem != null && selectedUIItem.IsFunction())
            {
                var caller = new List<CodeUIItem>();
                foreach (var item in edgeDict)
                {
                    var key = item.Key;
                    var edge = item.Value;
                    var srcItem = itemDict[key.Item1];
                    if (key.Item2 == selectedUIItem.GetUniqueName() && srcItem.IsFunction())
                    {
                        caller.Add(srcItem);
                    }
                }
                if (caller.Count == 1)
                {
                    UpdateCallOrderByItem(caller[0], ref orderEdge, true);
                }
            }

            foreach (var item in edgeDict)
            {
                var key = item.Key;
                var edge = item.Value;
                if (orderEdge.ContainsKey(key))
                {
                    edge.OrderData = orderEdge[key];
                }
                else
                {
                    edge.OrderData = null;
                }
            }
        }

        void UpdateCallOrderByItem(System.Windows.Shapes.Shape item,
            ref Dictionary<Tuple<string, string>, OrderData> orderEdge, bool onlyMarkRightSide = false)
        {
            // Select node item
            var scene = UIManager.Instance().GetScene();
            var itemDict = scene.GetItemDict();
            var isEdgeSelected = false;
            var edgeItem = item as CodeUIEdgeItem;
            if (edgeItem != null)
            {
                isEdgeSelected = true;
                if (itemDict.ContainsKey(edgeItem.m_srcUniqueName) &&
                    itemDict.ContainsKey(edgeItem.m_tarUniqueName))
                {
                    var srcItem = itemDict[edgeItem.m_srcUniqueName];
                    var dstItem = itemDict[edgeItem.m_tarUniqueName];
                    srcItem.m_isConnectedToFocusNode = true;
                    dstItem.m_isConnectedToFocusNode = true;
                    item = srcItem;
                }
            }

            var nodeItem = item as CodeUIItem;
            if (nodeItem == null)
            {
                return;
            }

            // Mark connected edges and nodes
            var edgeDict = scene.GetEdgeDict();
            var itemUniqueName = nodeItem.GetUniqueName();
            foreach (var edgePair in edgeDict)
            {
                var key = edgePair.Key;
                var edge = edgePair.Value;
                var srcItem = itemDict[key.Item1];
                var tarItem = itemDict[key.Item2];
                edge.m_isConnectedToFocusNode = key.Item1 == itemUniqueName || key.Item2 == itemUniqueName;

                if (isEdgeSelected == false)
                {
                    if (key.Item1 == itemUniqueName)// && tarItem.IsFunction())
                    {
                        tarItem.m_isConnectedToFocusNode = true;
                    }
                    if (!onlyMarkRightSide && key.Item2 == itemUniqueName)// && srcItem.IsFunction())
                    {
                        srcItem.m_isConnectedToFocusNode = true;
                    }
                }
            }

            if (!nodeItem.IsFunction())
            {
                return;
            }

            // Collect candidate edges
            var edgeList = new List<CodeUIEdgeItem>();
            double minXRange = double.MaxValue;
            double maxXRange = double.MinValue;
            foreach (var edgePair in edgeDict)
            {
                var key = edgePair.Key;
                var edge = edgePair.Value;
                var srcItem = itemDict[key.Item1];
                var tarItem = itemDict[key.Item2];
                if (key.Item1 == itemUniqueName)// && tarItem.IsFunction())
                {
                    edgeList.Add(edge);
                    Point srcPos, tarPos;
                    edge.GetNodePos(out srcPos, out tarPos);
                    minXRange = Math.Min(tarPos.X, minXRange);
                    maxXRange = Math.Max(tarPos.X, maxXRange);
                }
            }

            if (edgeList.Count <= 1)
            {
                return;
            }
            double basePos = 0.0;
            double itemX = nodeItem.Pos.X;
            if (minXRange < itemX && maxXRange > itemX)
            {
                basePos = double.NaN;
            }
            else if (minXRange >= itemX)
            {
                basePos = minXRange;
            }
            else if (maxXRange <= itemX)
            {
                basePos = maxXRange;
            }

            // Find edge order
            bool isSorted = false;
            string bodyCode = nodeItem.m_bodyCode;
            if (bodyCode != null && bodyCode != "")
            {
                var nodeDict = scene.GetItemDict();
                var edgePosList = new List<Tuple<CodeUIEdgeItem, int>>();
                foreach (var edge in edgeList)
                {
                    var tarItem = nodeDict[edge.m_tarUniqueName];
                    string indentifierPattern = string.Format(@"\b{0}\b", tarItem.GetName());

                    try
                    {
                        var match = Regex.Match(bodyCode, indentifierPattern, RegexOptions.ExplicitCapture);

                        if (match.Success)
                        {
                            edgePosList.Add(new Tuple<CodeUIEdgeItem, int>(edge, match.Index));
                        }
                        else
                        {
                            edgePosList.Add(new Tuple<CodeUIEdgeItem, int>(edge, -1));
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
                edgePosList.Sort((x, y) => x.Item2.CompareTo(y.Item2));
                edgeList.Clear();
                foreach (var edgePair in edgePosList)
                {
                    edgeList.Add(edgePair.Item1);
                }
                isSorted = true;
            }

            // Sort edges
            if (!isSorted)
            {
                edgeList.Sort((x, y) => x.ComparePos(y));
            }

            // Build call order data
            for (int i = 0; i < edgeList.Count; i++)
            {
                var edge = edgeList[i];
                Point srcPos, tarPos;
                edge.GetNodePos(out srcPos, out tarPos);
                double padding = srcPos.X < tarPos.X ? -12.0 : 12.0;
                double x, y;
                if (double.IsNaN(basePos))
                {
                    x = tarPos.X + padding;
                }
                else
                {
                    x = basePos + padding;
                }
                y = edge.FindCurveYPos(x);
                orderEdge[new Tuple<string, string>(edge.m_srcUniqueName, edge.m_tarUniqueName)] =
                    new OrderData(i + 1, new Point(x, y));
                //edge.m_orderData = new OrderData(i + 1, new Point(x, y));
            }
        }
    }
}
