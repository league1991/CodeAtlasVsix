using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    using EdgeKey = Tuple<string, string>;
    using ItemDict = Dictionary<string, CodeUIItem>;
    using EdgeDict = Dictionary<Tuple<string, string>, CodeUIEdgeItem>;
    using StopDict = Dictionary<string, string>;
    using System.Threading;
    using System.Windows.Data;
    using System.Windows.Controls;
    using System.Windows;
    using System.Windows.Shapes;

    public class CodeScene
    {
        ItemDict m_itemDict = new ItemDict();
        EdgeDict m_edgeDict = new EdgeDict();
        StopDict m_stopItem = new StopDict();
        CodeView m_view = null;
        SceneUpdateThread m_updateThread = null;
        object m_lockObj = new object();
        public bool m_isLayoutDirty = false;
        bool m_isSourceCandidate = true;
        List<EdgeKey> m_candidateEdge = new List<EdgeKey>();

        List<string> m_itemLruQueue = new List<string>();
        int m_lruMaxLength = 50;
        public int m_selectTimeStamp = 0;
        bool m_selectEventConnected = true;
        public bool m_autoFocus = true;
        bool m_autoFocusToggle = true;

        public CodeScene()
        {
            m_updateThread = new SceneUpdateThread(this);
            m_updateThread.Start();
        }
        
        public CodeView View
        {
            set { m_view = value; }
            get
            {
                return m_view;
            }
        }

        public bool IsAutoFocus()
        {
            return m_autoFocus && m_autoFocusToggle;
        }

        public void OnOpenDB()
        {
        }

        public string GetComment(string id)
        {
            // TODO: Add code
            return "";
        }

        #region data
        public ItemDict GetItemDict()
        {
            return m_itemDict;
        }

        public EdgeDict GetEdgeDict()
        {
            return m_edgeDict;
        }

        public CodeUIItem GetNode(string nodeID)
        {
            return m_itemDict[nodeID];
        }
        #endregion

        #region selection
        public bool GetSelectedCenter(out Point centerPnt)
        {
            centerPnt = new Point();
            int nCenter = 0;
            foreach (var item in m_itemDict)
            {
                if(item.Value.IsSelected)
                {
                    var pos = item.Value.Pos;
                    centerPnt.X += pos.X;
                    centerPnt.Y += pos.Y;
                    nCenter++;
                }
            }

            foreach(var edgeItem in m_edgeDict)
            {
                if (edgeItem.Value.IsSelected)
                {
                    var srcNode = m_itemDict[edgeItem.Key.Item1];
                    var tarNode = m_itemDict[edgeItem.Key.Item2];
                    centerPnt.X += (srcNode.Pos.X + tarNode.Pos.X) * 0.5;
                    centerPnt.Y += (srcNode.Pos.Y + tarNode.Pos.Y) * 0.5;
                    nCenter++;
                }
            }

            if(nCenter == 0)
            {
                return false;
            }
            centerPnt.X /= (double)nCenter;
            centerPnt.Y /= (double)nCenter;
            return true;
        }

        public void ClearSelection()
        {
            foreach (var item in m_itemDict)
            {
                item.Value.IsSelected = false;
            }

            foreach (var item in m_edgeDict)
            {
                item.Value.IsSelected = false;
            }
        }
        
        public List<CodeUIItem> SelectedNodes()
        {
            var items = new List<CodeUIItem>();
            foreach (var item in m_itemDict)
            {
                if (item.Value.IsSelected)
                {
                    items.Add(item.Value);
                }
            }
            return items;
        }

        public List<CodeUIEdgeItem> SelectedEdges()
        {
            var items = new List<CodeUIEdgeItem>();
            foreach (var item in m_edgeDict)
            {
                if (item.Value.IsSelected)
                {
                    items.Add(item.Value);
                }
            }
            return items;
        }

        public List<Shape> SelectedItems()
        {
            var items = new List<Shape>();
            foreach (var item in m_itemDict)
            {
                if (item.Value.IsSelected)
                {
                    items.Add(item.Value);
                }
            }
            foreach (var item in m_edgeDict)
            {
                if (item.Value.IsSelected)
                {
                    items.Add(item.Value);
                }
            }
            return items;
        }

        public bool SelectCodeItem(string uniqueName)
        {
            if (!m_itemDict.ContainsKey(uniqueName))
            {
                return false;
            }
            m_itemDict[uniqueName].IsSelected = true;
            return true;
        }

        public bool SelectOneItem(Shape item)
        {
            ClearSelection();
            var node = item as CodeUIItem;
            var edge = item as CodeUIEdgeItem;
            if (node != null)
            {
                node.IsSelected = true;
                return true;
            }
            else if (edge != null)
            {
                edge.IsSelected = true;
                return false;
            }
            return false;
        }

        public bool SelectOneEdge(CodeUIEdgeItem edge)
        {
            edge.IsSelected = true;
            return true;
        }

        public bool SelectNearestItem(Point pos)
        {
            double minDist = 1e12;
            CodeUIItem minItem = null;
            foreach (var pair in m_itemDict)
            {
                var dPos = pair.Value.Pos - pos;
                double dist = dPos.LengthSquared;
                if (dist < minDist)
                {
                    minDist = dist;
                    minItem = pair.Value;
                }
            }

            if (minItem != null)
            {
                return SelectOneItem(minItem);
            }
            else
            {
                return false;
            }
        }
        
        public bool OnSelectItems()
        {
            if (!m_selectEventConnected)
            {
                return false;
            }

            var itemList = SelectedItems();
            m_selectTimeStamp += 1;

            foreach (var item in itemList)
            {
                var uiItem = item as CodeUIItem;
                if (uiItem == null)
                {
                    continue;
                }

                uiItem.m_selectCounter += 1;
                uiItem.m_selectTimeStamp = m_selectTimeStamp;
                UpdateLRU(new List<string> { uiItem.GetUniqueName() });
            }

            RemoveItemLRU();
            // TODO: more code
            return true;
        }
        #endregion

        #region Navigation
        public void UpdateCandidateEdge()
        {
            CodeUIEdgeItem centerItem = null;
            foreach (var item in m_edgeDict)
            {
                var edgeKey = item.Key;
                var edge = item.Value;
                edge.IsCandidate = false;
                if (edge.IsSelected)
                {
                    centerItem = edge;
                }
            }

            if (centerItem == null)
            {
                return;
            }

            m_candidateEdge.Clear();
            var srcEdgeList = new List<EdgeKey>();
            var tarEdgeList = new List<EdgeKey>();
            var srcNode = m_itemDict[centerItem.m_srcUniqueName];
            var tarNode = m_itemDict[centerItem.m_tarUniqueName];
            foreach (var item in m_edgeDict)
            {
                var edgeKey = item.Key;
                var edge = item.Value;
                if (edge == centerItem)
                {
                    continue;
                }
                if (edgeKey.Item1 == centerItem.m_srcUniqueName)
                {
                    srcEdgeList.Add(edgeKey);
                }
                else if (edgeKey.Item2 == centerItem.m_tarUniqueName && edgeKey.Item1 != centerItem.m_tarUniqueName)
                {
                    tarEdgeList.Add(edgeKey);
                }
            }

            m_isSourceCandidate = true;
            if (tarEdgeList.Count == 0 && srcEdgeList.Count > 0)
            {
                m_candidateEdge = srcEdgeList;
            }
            else if (srcEdgeList.Count == 0 && tarEdgeList.Count > 0)
            {
                m_candidateEdge = tarEdgeList;
                m_isSourceCandidate = false;
            }
            else if (tarNode.m_selectTimeStamp > srcNode.m_selectTimeStamp)
            {
                m_candidateEdge = tarEdgeList;
                m_isSourceCandidate = false;
            }
            else
            {
                m_candidateEdge = srcEdgeList;
            }

            foreach (var edgeKey in m_candidateEdge)
            {
                if (m_edgeDict.ContainsKey(edgeKey))
                {
                    var edge = m_edgeDict[edgeKey];
                    edge.IsCandidate = true;
                }
            }
            foreach (var item in m_edgeDict)
            {
                item.Value.UpdateStroke();
            }
        }

        public void FindNeighbour(Vector mainDirection)
        {
            Console.WriteLine("find neighbour:" + mainDirection.ToString());
            var itemList = SelectedItems();
            if (itemList.Count == 0)
            {
                return;
            }
            // AcquireLock();
            var centerItem = itemList[0];
            var centerNode = centerItem as CodeUIItem;
            var centerEdge = centerItem as CodeUIEdgeItem;
            Shape minItem = null;
            if (centerNode != null)
            {
                minItem = FindNeighbourForNode(centerNode, mainDirection);
            }
            else if (centerEdge != null)
            {
                minItem = FindNeighbourForEdge(centerEdge, mainDirection);
            }
            // ReleaseLock();

            if (minItem == null)
            {
                return;
            }

            SelectOneItem(minItem);
        }

        public Shape FindNeighbourForEdge(CodeUIEdgeItem centerItem, Vector mainDirection)
        {
            if (m_isSourceCandidate && centerItem.m_orderData != null && Math.Abs(mainDirection.Y) > 0.8)
            {
                var srcItem = m_itemDict[centerItem.m_srcUniqueName];
                var tarItem = m_itemDict[centerItem.m_tarUniqueName];
                if (srcItem != null && tarItem != null && srcItem.IsFunction() && tarItem.IsFunction())
                {
                    var tarOrder = centerItem.m_orderData.m_order - 1;
                    if (mainDirection.Y > 0)
                    {
                        tarOrder = centerItem.m_orderData.m_order + 1;
                    }
                    foreach (var edgePair in m_candidateEdge)
                    {
                        if (m_edgeDict.ContainsKey(edgePair))
                        {
                            var edge = m_edgeDict[edgePair];
                            if (edge.m_srcUniqueName == centerItem.m_srcUniqueName &&
                                edge.m_orderData != null && edge.m_orderData.m_order == tarOrder)
                            {
                                return edge;
                            }
                        }
                    }
                }
                return null;
            }

            var srcNode = GetNode(centerItem.m_srcUniqueName);
            var tarNode = GetNode(centerItem.m_tarUniqueName);
            var nCommonIn = 0;
            var nCommonOut = 0;
            foreach (var edgeKey in m_candidateEdge)
            {
                if (edgeKey.Item1 == centerItem.m_srcUniqueName)
                {
                    nCommonIn++;
                }
                if (edgeKey.Item2 == centerItem.m_tarUniqueName)
                {
                    nCommonOut++;
                }
            }

            double percent = 0.5;
            if (m_isSourceCandidate)
            {
                percent = 0.3;
            }
            else
            {
                percent = 0.7;
            }
            var centerPos = centerItem.PointAtPercent(percent);

            Point srcPos, tarPos;
            centerItem.GetNodePos(out srcPos, out tarPos);
            var edgeDir = tarPos - srcPos;
            edgeDir.Normalize();
            var proj = Vector.Multiply(mainDirection, edgeDir);

            if (Math.Abs(mainDirection.X) > 0.8)
            {
                if (proj > 0.0 && tarNode != null)
                {
                    return tarNode;
                }
                else if (proj < 0.0 && srcNode != null)
                {
                    return srcNode;
                }
            }

            // Find nearest edge
            var minEdgeVal = 1e12;
            CodeUIEdgeItem minEdge = null;
            var centerKey = new EdgeKey(centerItem.m_srcUniqueName, centerItem.m_tarUniqueName);
            foreach (var edgeKey in m_candidateEdge)
            {
                CodeUIEdgeItem item = null;
                if (!m_edgeDict.TryGetValue(edgeKey, out item))
                {
                    continue;
                }
                if (item == centerItem )
                {
                    continue;
                }

                bool isEdgeKey0InCenterKey = edgeKey.Item1 == centerKey.Item1 || edgeKey.Item1 == centerKey.Item2;
                bool isEdgeKey1InCenterKey = edgeKey.Item2 == centerKey.Item1 || edgeKey.Item2 == centerKey.Item2;
                if (!(isEdgeKey0InCenterKey || isEdgeKey1InCenterKey))
                {
                    continue;
                }
                var y = item.FindCurveYPos(centerPos.X);
                var dPos = new Point(centerPos.X, y) - centerPos;
                var cosVal = Vector.Multiply(dPos, mainDirection) / (dPos.Length + 1e-5);
                if (cosVal < 0.0)
                {
                    continue;
                }

                var xProj = dPos.X * mainDirection.X + dPos.Y * mainDirection.Y;
                var yProj = dPos.X * mainDirection.Y - dPos.Y * mainDirection.X;

                xProj /= 2.0;
                var dist = xProj * xProj + yProj * yProj;
                if (dist < minEdgeVal)
                {
                    minEdgeVal = dist;
                    minEdge = item;
                }
            }

            if (minEdge != null)
            {
                return minEdge;
            }

            // Find nearest node
            var minNodeValConnected = 1e12;
            CodeUIItem minNodeConnected = null;
            var minNodeVal = 1e12;
            CodeUIItem minNode = null;
            minEdgeVal *= 3;
            minNodeVal *= 2;

            var valList = new List<double> { minEdgeVal, minNodeVal, minNodeValConnected };
            var itemList = new List<Shape> { minEdge, minNode, minNodeConnected };
            Shape minItem = null;
            var minItemVal = 1e12;
            for (int i = 0; i < valList.Count; i++)
            {
                if (valList[i] < minItemVal)
                {
                    minItemVal = valList[i];
                    minItem = itemList[i];
                }
            }
            return minItem;
        }

        public Shape FindNeighbourForNode(CodeUIItem centerItem, Vector mainDirection)
        {
            var centerPos = centerItem.Pos;
            var centerUniqueName = centerItem.GetUniqueName();

            if (centerItem.IsFunction())
            {
                if (mainDirection.X > 0.8)
                {
                    foreach (var item in m_edgeDict)
                    {
                        if (item.Key.Item1 == centerItem.GetUniqueName())
                        {
                            return item.Value;
                        }
                    }
                }
            }

            // find nearest edge
            var minEdgeValConnected = 1.0e12;
            CodeUIEdgeItem minEdgeConnected = null;
            var minEdgeVal = 1.0e12;
            CodeUIEdgeItem minEdge = null;
            foreach (var edgePair in m_edgeDict)
            {
                var edgeKey = edgePair.Key;
                var item = edgePair.Value;
                var dPos = item.GetMiddlePos() -centerPos;
                var cosVal = Vector.Multiply(dPos, mainDirection) / dPos.Length;
                if (cosVal < 0.2)
                {
                    continue;
                }

                var xProj = dPos.X * mainDirection.X + dPos.Y * mainDirection.Y;
                var yProj = dPos.X * mainDirection.Y - dPos.Y * mainDirection.X;
                xProj /= 3.0;
                var dist = xProj * xProj + yProj * yProj;
                if (centerUniqueName == edgeKey.Item1 ||
                    centerUniqueName == edgeKey.Item2)
                {
                    if (dist < minEdgeValConnected)
                    {
                        minEdgeValConnected = dist;
                        minEdgeConnected = item;
                    }
                }
                else if (dist < minEdgeVal)
                {
                    minEdgeVal = dist;
                    minEdge = item;
                }
            }

            // find nearest node
            var minNodeValConnected = 1e12;
            CodeUIItem minNodeConnected = null;
            var minNodeVal = 1e12;
            CodeUIItem minNode = null;
            foreach (var itemPair in m_itemDict)
            {
                var uname = itemPair.Key;
                var item = itemPair.Value;
                if (item == centerItem)
                {
                    continue;
                }

                var dPos = item.Pos - centerPos;
                var cosVal = Vector.Multiply(dPos, mainDirection) / dPos.Length;
                if (cosVal < 0.6)
                {
                    continue;
                }

                var xProj = dPos.X * mainDirection.X + dPos.Y * mainDirection.Y;
                var yProj = dPos.X * mainDirection.Y - dPos.Y * mainDirection.X;
                xProj /= 3.0;
                var dist = xProj * xProj + yProj * yProj;

                // Check if connected with current item
                var isEdged = false;
                foreach (var edgePair in m_edgeDict)
                {
                    if ((centerUniqueName == edgePair.Key.Item1 ||
                        centerUniqueName == edgePair.Key.Item2) && 
                        (uname == edgePair.Key.Item1 ||
                        uname == edgePair.Key.Item2))
                    {
                        isEdged = true;
                    }
                }

                if (isEdged)
                {
                    if (dist < minNodeValConnected)
                    {
                        minNodeValConnected = dist;
                        minNodeConnected = item;
                    }
                }
                else
                {
                    if (dist < minNodeVal)
                    {
                        minNodeVal = dist;
                        minNode = item;
                    }
                }
            }

            minEdgeVal *= 3;
            minNodeVal *= 2;

            // Choose edge first in x direction
            if (Math.Abs(mainDirection.X) > 0.8)
            {
                if (minEdgeConnected != null)
                {
                    return minEdgeConnected;
                }
                else if (minEdge != null)
                {
                    return minEdge;
                }
                else if (minNodeConnected != null)
                {
                    return minNodeConnected;
                }
                else if (minNode != null)
                {
                    return minNode;
                }
            }

            // Choose item first in y direction
            if (Math.Abs(mainDirection.Y) > 0.8)
            {
                if (minNode != null)
                {
                    return minNode;
                }
                else if (minNodeConnected != null)
                {
                    return minNodeConnected;
                }
                else if (minEdgeConnected != null)
                {
                    return minEdgeConnected;
                }
                else if (minEdge != null)
                {
                    return minEdge;
                }
            }

            var valList = new List<double> { minEdgeVal, minEdgeValConnected, minNodeVal, minNodeValConnected};
            var itemList = new List<Shape> { minEdge, minEdgeConnected, minNode, minNodeConnected };
            Shape minItem = null;
            var minItemVal = 1e12;
            for (int i = 0; i < valList.Count; i++)
            {
                if (valList[i] < minItemVal)
                {
                    minItemVal = valList[i];
                    minItem = itemList[i];
                }
            }

            return minItem;
        }
        #endregion

        public void MoveItems()
        {
            if(m_view == null)
            {
                return;
            }
            m_view.Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                AcquireLock();
                foreach (var node in m_itemDict)
                {
                    var item = node.Value;
                    item.MoveToTarget(0.05);
                }
                ReleaseLock();
            });
        }

        #region ThreadSync
        public void AcquireLock()
        {
            Monitor.Enter(m_lockObj);
        }

        public void ReleaseLock()
        {
            Monitor.Exit(m_lockObj);
        }
        #endregion

        #region LRU
        void DeleteLRU(List<string> itemKeyList)
        {
            foreach(var itemKey in itemKeyList)
            {
                m_itemLruQueue.Remove(itemKey);
            }
        }

        void UpdateLRU(List<string> itemKeyList)
        {
            var deleteKeyList = new List<string>();

            foreach(var itemKey in itemKeyList)
            {
                int idx = m_itemLruQueue.FindIndex(x => x == itemKey);

                if( idx == -1)
                {
                    if(m_itemLruQueue.Count > m_lruMaxLength)
                    { }
                }
                else
                {
                    m_itemLruQueue.RemoveAt(idx);
                }

                m_itemLruQueue.Insert(0, itemKey);
            }
        }

        void RemoveItemLRU()
        {
            m_selectEventConnected = false;
            if(m_itemLruQueue.Count > m_lruMaxLength)
            {
                while (m_itemLruQueue.Count > m_lruMaxLength)
                {
                    _DoDeleteCodeItem(m_itemLruQueue[m_lruMaxLength]);
                    m_itemLruQueue.RemoveAt(m_lruMaxLength);
                }
            }
            m_selectEventConnected = true;
        }
        #endregion
        
        #region Add/Delete Item and Edge
        bool _DoAddCodeItem(string srcUniqueName)
        {
            if (m_itemDict.ContainsKey(srcUniqueName))
            {
                return false;
            }
            var item = new CodeUIItem(srcUniqueName);
            m_itemDict[srcUniqueName] = item;
            m_view.canvas.Children.Add(item);
            Point center;
            GetSelectedCenter(out center);
            item.Pos = center;
            item.SetTargetPos(center);
            m_isLayoutDirty = true;
            return true;
        }

        void _DoDeleteCodeItem(string uniqueName)
        {
            if(!m_itemDict.ContainsKey(uniqueName))
            {
                return;
            }

            List<EdgeKey> deleteEdges = new List<EdgeKey>();
            foreach(var edge in m_edgeDict)
            {
                if(edge.Key.Item1 == uniqueName || edge.Key.Item2 == uniqueName)
                {
                    deleteEdges.Add(edge.Key);
                }
            }

            foreach(var edgeKey in deleteEdges)
            {
                _DoDeleteCodeEdgeItem(edgeKey);
            }

            m_view.canvas.Children.Remove(m_itemDict[uniqueName]);
            m_itemDict.Remove(uniqueName);
            m_isLayoutDirty = true;
        }

        void _DoDeleteCodeEdgeItem(EdgeKey edgeKey)
        {
            if (!m_edgeDict.ContainsKey(edgeKey))
            {
                return;
            }

            m_view.canvas.Children.Remove(m_edgeDict[edgeKey]);
            m_edgeDict.Remove(edgeKey);
            m_isLayoutDirty = true;
        }

        bool _DoAddCodeEdgeItem(string srcUniqueName, string tarUniqueName, object data = null)
        {
            var key = new EdgeKey(srcUniqueName, tarUniqueName);
            if (m_edgeDict.ContainsKey(key))
            {
                return false;
            }

            if(!m_itemDict.ContainsKey(srcUniqueName) ||
                !m_itemDict.ContainsKey(tarUniqueName))
            {
                return false;
            }

            var srcNode = m_itemDict[srcUniqueName];
            var tarNode = m_itemDict[tarUniqueName];
            var edgeItem = new CodeUIEdgeItem(srcUniqueName, tarUniqueName);
            //var srcBinding = new Binding("RightPoint") { Source = srcNode };
            //var tarBinding = new Binding("LeftPoint") { Source = tarNode };
            //BindingOperations.SetBinding(edgeItem, CodeUIEdgeItem.StartPointProperty, srcBinding);
            //BindingOperations.SetBinding(edgeItem, CodeUIEdgeItem.EndPointProperty, tarBinding);
            m_edgeDict.Add(key, edgeItem);
            if(data != null)
            {
                // TODO: add custom edge data
            }
            m_view.canvas.Children.Add(edgeItem);
            m_isLayoutDirty = true;
            return true;
        }

        public void AddCodeItem(string srcUniqueName)
        {
            AcquireLock();
            _DoAddCodeItem(srcUniqueName);
            UpdateLRU(new List<string> { srcUniqueName});
            RemoveItemLRU();
            ReleaseLock();
        }

        public bool AddCodeEdgeItem(string srcUniqueName, string tarUniqueName)
        {
            return _DoAddCodeEdgeItem(srcUniqueName, tarUniqueName);
        }

        public void DeleteCodeItem(string uniqueName)
        {
            AcquireLock();
            _DoDeleteCodeItem(uniqueName);
            RemoveItemLRU();
            ReleaseLock();
        }
        
        public void DeleteSelectedItems(bool addToStop = false)
        {
            AcquireLock();

            var itemList = new List<string>();
            Point lastPos = new Point(Double.NaN, Double.NaN);
            foreach (var item in m_itemDict)
            {
                if (item.Value.IsSelected)
                {
                    itemList.Add(item.Key);
                    lastPos = item.Value.Pos;
                    if (addToStop)
                    {
                        m_stopItem.Add(item.Key, item.Value.Name);
                    }
                }
            }

            foreach (var item in m_edgeDict)
            {
                var edge = item.Value;
                if (edge.IsSelected)
                {
                    var srcItem = m_itemDict[item.Key.Item1];
                    lastPos = srcItem.Pos;
                    break;
                }
            }

            CodeUIEdgeItem lastFunction = null;
            if (itemList.Count == 1 && m_itemDict[itemList[0]].IsFunction())
            {
                var funItem = m_itemDict[itemList[0]];
                Tuple<string, string> callEdgeKey = null;
                CodeUIEdgeItem callEdge = null;
                int order = -1;
                foreach (var item in m_edgeDict)
                {
                    if (item.Key.Item1 == funItem.GetUniqueName())
                    {
                        callEdgeKey = item.Key;
                        callEdge = item.Value;
                        order = callEdge.GetCallOrder();
                        break;
                    }
                }

                if (callEdgeKey != null && callEdge != null && order != -1)
                {
                    foreach (var item in m_edgeDict)
                    {
                        if (item.Key.Item1 == callEdgeKey.Item1 && item.Value.GetCallOrder() == order+1)
                        {
                            lastFunction = item.Value;
                            break;
                        }
                    }
                }

            }
            if (itemList != null)
            {
                foreach (var item in itemList)
                {
                    _DoDeleteCodeItem(item);
                }
                DeleteLRU(itemList);
                RemoveItemLRU();
            }

            var edgeList = new List<Tuple<string, string>>();
            foreach (var item in m_edgeDict)
            {
                if (item.Value.IsSelected)
                {
                    edgeList.Add(item.Key);
                }
            }
            foreach (var edgeKey in edgeList)
            {
                _DoDeleteCodeEdgeItem(edgeKey);
            }

            if (lastFunction != null)
            {
                SelectOneEdge(lastFunction);
            }
            else if (lastPos.X != Double.NaN)
            {
                SelectNearestItem(lastPos);
            }

            ReleaseLock();
        }
        #endregion

        #region Add references
        List<string> _AddRefs(string refStr, string entStr, bool inverseEdge = false, int maxCount = -1)
        {
            var dbObj = DBManager.Instance().GetDB();
            var itemList = SelectedNodes();

            var refNameList = new List<string>();
            foreach (var item in itemList)
            {
                var uniqueName = item.GetUniqueName();
                var entList = new List<DoxygenDB.Entity>();
                var refList = new List<DoxygenDB.Reference>();
                dbObj.SearchRefEntity(out entList, out refList, uniqueName, refStr, entStr);

                // Add to candidate
                var candidateList = new List<Tuple<string, DoxygenDB.Reference, int>>();
                for (int i = 0; i < entList.Count; i++)
                {
                    var entObj = entList[i];
                    var refObj = refList[i];
                    var entName = entObj.UniqueName();
                    // Get lines
                    var metricRes = entObj.Metric();
                    DoxygenDB.Variant metricLine;
                    int line;
                    if (metricRes.TryGetValue("CountLine", out metricLine))
                    {
                        line = metricLine.m_int;
                    }
                    else
                    {
                        line = 0;
                    }
                    candidateList.Add(new Tuple<string, DoxygenDB.Reference, int>(entName, refObj, line));
                }

                // Sort candidate
                if (maxCount > 0)
                {
                    candidateList.Sort((x, y) => -x.Item3.CompareTo(y.Item3));
                }

                var addedList = new List<string>();
                for (int ithCan = 0; ithCan < candidateList.Count; ithCan++)
                {
                    var candidate = candidateList[ithCan];
                    var canEntName = candidate.Item1;
                    var canRefObj = candidate.Item2;

                    bool res = _DoAddCodeItem(canEntName);
                    if (res)
                    {
                        addedList.Add(canEntName);
                    }
                    if (inverseEdge)
                    {
                        _DoAddCodeEdgeItem(uniqueName, canEntName, new Dictionary<string, object> { { "dbRef", canRefObj } });
                    }
                    else
                    {
                        _DoAddCodeEdgeItem(canEntName, uniqueName, new Dictionary<string, object> { { "dbRef", canRefObj } });
                    }

                    if (maxCount > 0 && addedList.Count >= maxCount)
                    {
                        break;
                    }
                }
                refNameList.AddRange(addedList);
            }
            return refNameList;
        }

        public void AddRefs(string refStr, string entStr, bool inverseEdge = false, int maxCount = -1)
        {
            AcquireLock();
            Point center;
            var res = GetSelectedCenter(out center);
            var refNameList = _AddRefs(refStr, entStr, inverseEdge, maxCount);
            UpdateLRU(refNameList);
            RemoveItemLRU();
            if (res)
            {
                SelectNearestItem(center);
            }
            ReleaseLock();
        }
        #endregion

        public void Invalidate()
        {
            foreach(var node in m_itemDict)
            {
                node.Value.Invalidate();
            }
            
            foreach(var edge in m_edgeDict)
            {
                edge.Value.Invalidate();
            }


            foreach (var node in m_itemDict)
            {
                node.Value.IsDirty = false;
            }

            foreach (var edge in m_edgeDict)
            {
                edge.Value.IsDirty = false;
            }
        }
    }
}
