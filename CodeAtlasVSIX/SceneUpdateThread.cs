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
        string m_curDocPath = "";
        int m_curDocline = -1;

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
            DateTime saveTime = DateTime.Now;
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

                    if (scene.m_traceCursorUpdate != SyncToEditorType.SYNC_NONE)
                    {
                        TraceCursor();
                    }

                    int layoutTime = 0;
                    if (scene.m_isLayoutDirty)
                    {
                        scene.AcquireLock();
                        BeginTimeStamp();
                        if (scene.m_layoutType == LayoutType.LAYOUT_GRAPH)
                        {
                            UpdateLayeredLayoutWithComp();
                            scene.m_isLayoutDirty = false;
                        }
                        else if(scene.m_layoutType == LayoutType.LAYOUT_FORCE)
                        {
                            var dist = UpdateForceDirectedLayout();
                            scene.m_isLayoutDirty = (dist > 0.5);
                        }

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

                    // Save configuration every 10 minutes
                    if ((DateTime.Now - saveTime).TotalSeconds > 10 * 60 && mainUI.GetCommandActive())
                    {
                        scene.SaveConfig();
                        saveTime = DateTime.Now;
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

        double UpdateForceDirectedLayout()
        {
            var scene = UIManager.Instance().GetScene();
            var itemDict = scene.GetItemDict();
            var edgeDict = scene.GetEdgeDict();

            double totalDist = 0;
            foreach (var pairA in itemDict)
            {
                foreach (var pairB in itemDict)
                {
                    if (pairA.Key == pairB.Key)
                    {
                        continue;
                    }
                    var itemA = pairA.Value;
                    var itemB = pairB.Value;
                    Size sizeA = new Size(itemA.GetWidth(), itemA.GetHeight());
                    Size sizeB = new Size(itemB.GetWidth(), itemB.GetHeight());
                    Point positionA = itemA.GetTargetPos();
                    Point positionB = itemB.GetTargetPos();
                    double xOverlap = (sizeA.Width  + sizeB.Width)  * 0.5 + 120 - Math.Abs(positionB.X - positionA.X);
                    double yOverlap = (sizeA.Height + sizeB.Height) * 0.5 + 4 - Math.Abs(positionB.Y - positionA.Y);
                    if (xOverlap > 0 && yOverlap > 0)
                    {
                        //double xOffset = (positionB.X > positionA.X) ? -xOverlap : xOverlap;
                        //const double xFactor = 0.001;
                        //positionA.X += xOffset * 0.5 * xFactor;
                        //positionB.X += -xOffset * 0.5 * xFactor;
                        //totalDist += xOverlap * xFactor;

                        double yOffset = (positionB.Y > positionA.Y) ? -yOverlap : yOverlap;
                        const double yFactor = 1.1;
                        positionA.Y += yOffset * 0.5 * yFactor;
                        positionB.Y += -yOffset * 0.5 * yFactor;
                        totalDist += yOverlap * yFactor;
                    }
                    itemA.SetTargetPos(positionA);
                    itemB.SetTargetPos(positionB);
                }
            }
            return totalDist;
        }

        class EdgeOrderData
        {
            public EdgeOrderData(Node node, int order)
            {
                m_node = node;
                m_order = order;
            }
            public Node m_node;
            public int m_order;
        };

        void TraceCursor()
        {
            var mainUI = UIManager.Instance().GetMainUI();
            var scene = UIManager.Instance().GetScene();
            mainUI.Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                string curPath;
                int curLine, curCol;
                CursorNavigator.GetCursorPosition(out curPath, out curLine, out curCol);
                if (curPath != m_curDocPath || curLine != m_curDocline)
                {
                    if (Math.Abs(curLine - m_curDocline) > 1)
                    {
                        mainUI.DoShowInAtlas(false);
                        if (scene.m_traceCursorUpdate == SyncToEditorType.SYNC_CURSOR_CALLER_CALLEE)
                        {
                            mainUI.OnFindCallers(null, null);
                            mainUI.OnFindCallees(null, null);
                        }
                    }
                    m_curDocPath = curPath;
                    m_curDocline = curLine;
                }
            }, DispatcherPriority.Loaded);
        }

        void UpdateLayeredLayoutWithComp()
        {
            var scene = UIManager.Instance().GetScene();

            Graph graph = new Graph();

            var itemDict = scene.GetItemDict();
            var edgeDict = scene.GetEdgeDict();
            var nodeDict = new Dictionary<string, Node>();
            var edgeOrderDict = new Dictionary<string, List<EdgeOrderData>>();     // store edge order
            var bookmarkOrderDict = new Dictionary<string, List<EdgeOrderData>>();
            foreach (var item in itemDict)
            {
                var node = graph.AddNode(item.Key);
                nodeDict[item.Key] = node;
            }
            foreach (var edge in edgeDict)
            {
                var key = edge.Key;
                var edgeObj = edge.Value;
                graph.AddEdge(key.Item1, key.Item2);
                if (!edgeOrderDict.ContainsKey(key.Item1))
                {
                    edgeOrderDict[key.Item1] = new List<EdgeOrderData>();
                }
                if (edgeObj.OrderData != null && edgeObj.OrderData.m_order > 0)
                {
                    var node = nodeDict[key.Item2];
                    edgeOrderDict[key.Item1].Add(new EdgeOrderData(node, edgeObj.OrderData.m_order));
                }

                var srcNode = itemDict[key.Item1];
                if (srcNode.GetKind() == DoxygenDB.EntKind.PAGE)
                {
                    var node = nodeDict[key.Item1];

                    if (!bookmarkOrderDict.ContainsKey(key.Item2))
                    {
                        bookmarkOrderDict[key.Item2] = new List<EdgeOrderData>();
                    }

                    var srcMetric = srcNode.GetMetric();
                    var file = srcMetric["file"].m_string;
                    var line = srcMetric["line"].m_int;
                    long order = (file.GetHashCode() & 0x7fff0000) + line;
                    bookmarkOrderDict[key.Item2].Add(new EdgeOrderData(node, (int)order));
                }
            }
            // Sort edge order
            foreach (var item in edgeOrderDict)
            {
                item.Value.Sort((x, y) => x.m_order.CompareTo(y.m_order));
            }
            foreach (var item in bookmarkOrderDict)
            {
                item.Value.Sort((x, y) => x.m_order.CompareTo(y.m_order));
            }
            graph.Attr.LayerDirection = LayerDirection.LR;
            graph.CreateGeometryGraph();

            // Set graph settings
            var layerSetting = graph.LayoutAlgorithmSettings as SugiyamaLayoutSettings;
            if (layerSetting != null)
            {
                layerSetting.LayerSeparation = 120;
                layerSetting.NodeSeparation = 4;
                foreach (var orderItem in edgeOrderDict)
                {
                    var orderList = orderItem.Value;
                    for (int ithOrder = 0; ithOrder < orderList.Count-1; ithOrder++)
                    {
                        var prevNode = orderList[ithOrder].m_node.GeometryNode;
                        var nextNode = orderList[ithOrder+1].m_node.GeometryNode;
                        layerSetting.AddLeftRightConstraint(prevNode, nextNode);
                    }
                }
                foreach (var orderItem in bookmarkOrderDict)
                {
                    var orderList = orderItem.Value;
                    for (int ithOrder = 0; ithOrder < orderList.Count - 1; ithOrder++)
                    {
                        var prevNode = orderList[ithOrder].m_node.GeometryNode;
                        var nextNode = orderList[ithOrder + 1].m_node.GeometryNode;
                        layerSetting.AddLeftRightConstraint(prevNode, nextNode);
                    }
                }
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
                else if(edge.OrderData != null)
                {
                    edge.OrderData.m_isVisible = false;
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
