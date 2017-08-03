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
    using DataDict = Dictionary<string, object>;
    using System.Threading;
    using System.Windows.Data;
    using System.Windows.Controls;
    using System.Windows;
    using System.Windows.Shapes;
    using System.Windows.Media;
    using System.IO;
    using System.Web.Script.Serialization;
    using System.Collections;
    using Microsoft.VisualStudio.Shell;
    using EnvDTE;
    using EnvDTE80;
    using System.Windows.Threading;

    public class SchemeData
    {
        public List<string> m_nodeList = new List<string>();
        public Dictionary<EdgeKey, DataDict> m_edgeDict = new Dictionary<EdgeKey, DataDict>();
        public bool IsEmpty() { return m_nodeList.Count == 0 && m_edgeDict.Count == 0; }
    }

    public class CodeScene: DispatcherObject
    {
        #region Data Member
        // Data
        ItemDict m_itemDict = new ItemDict();
        EdgeDict m_edgeDict = new EdgeDict();
        StopDict m_stopItem = new StopDict();
        Dictionary<string, DataDict> m_itemDataDict = new Dictionary<string, DataDict>();
        Dictionary<EdgeKey, DataDict> m_edgeDataDict = new Dictionary<EdgeKey, DataDict>();
        Dictionary<string, SchemeData> m_scheme = new Dictionary<string, SchemeData>();
        List<string> m_curValidScheme = new List<string>();
        List<Color> m_curValidSchemeColor = new List<Color>();
        CodeView m_view = null;
        
        // Thread
        SceneUpdateThread m_updateThread = null;
        object m_lockObj = new object();
        
        // Layout/UI Status
        public bool m_isLayoutDirty = false;
        public bool m_isInvalidate = false;
        bool m_isSourceCandidate = true;
        List<EdgeKey> m_candidateEdge = new List<EdgeKey>();
        bool m_selectEventConnected = true;
        bool m_autoFocusToggle = true;
        public double m_itemMoveDistance = 0.0;
        public bool m_waitingItemMove = false;
        public int m_selectTimeStamp = 0;
        public int m_schemeTimeStamp = 0;
        public string m_customEdgeSource = "";

        // LRU
        List<string> m_itemLruQueue = new List<string>();
        int m_lruMaxLength = 100;
        #endregion

        public CodeScene()
        {
            m_updateThread = new SceneUpdateThread(this);
            m_updateThread.Start();
        }

        public void OnDestroyScene()
        {
            if (m_updateThread != null)
            {
                m_updateThread.Terminate();
            }
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
            return m_autoFocusToggle;
        }
        
        void AddOrReplaceDict(DataDict dict, string key, object value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
            }
            else
            {
                dict.Add(key, value);
            }
        }

        #region Read/Write Data
        public void OnOpenDB()
        {
            var dbObj = DBManager.Instance().GetDB();
            var mainUI = UIManager.Instance().GetMainUI();
            var dbPath = dbObj.GetDBPath();
            if (dbPath == null || dbPath == "")
            {
                mainUI.SetCommandActive(true);
                return;
            }

            var configPath = dbPath + ".config";
            string jsonStr = "";
            if (File.Exists(configPath))
            {
                jsonStr = File.ReadAllText(configPath);
            }
            if (jsonStr == "")
            {
                mainUI.SetCommandActive(true);
                return;
            }

            mainUI.SetCommandActive(false);
            m_updateThread.SetForceSleepTime(100);

            System.Threading.Thread addingThread = new System.Threading.Thread((ThreadStart)delegate
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var sceneData = js.Deserialize<Dictionary<string, object>>(jsonStr);

                AcquireLock();
                Logger.Info("Open File: " + configPath);
                var t0 = DateTime.Now;
                var t1 = t0;
                var beginTime = t0;

                // Stop item
                var stopItemData = sceneData["stopItem"] as Dictionary<string, object>;
                foreach (var item in stopItemData)
                {
                    m_stopItem[item.Key] = item.Value as string;
                }

                // Item Data
                var itemDataDict = sceneData["codeData"] as Dictionary<string, object>;
                foreach (var itemPair in itemDataDict)
                {
                    var itemData = itemPair.Value as DataDict;
                    m_itemDataDict[itemPair.Key] = itemData;
                }

                // edge data
                var edgeData = sceneData["edgeData"] as ArrayList;
                foreach (var dataItem in edgeData)
                {
                    var dataList = dataItem as ArrayList;
                    var edgeKey = new EdgeKey(dataList[0] as string, dataList[1] as string);
                    var edgeDataDict = dataList[2] as DataDict;
                    m_edgeDataDict[edgeKey] = edgeDataDict;
                }
                t1 = DateTime.Now;
                Logger.Debug("--------------Edgedata " + (t1 - t0).TotalMilliseconds.ToString());
                t0 = t1;

                // scheme
                var schemeDict = sceneData["scheme"] as Dictionary<string, object>;
                foreach (var schemeItem in schemeDict)
                {
                    var name = schemeItem.Key;
                    var schemeData = schemeItem.Value as Dictionary<string, object>;
                    var nodeList = schemeData["node"] as ArrayList;
                    var edgeList = schemeData["edge"] as ArrayList;
                    var schemeObj = new SchemeData();
                    foreach (var node in nodeList)
                    {
                        schemeObj.m_nodeList.Add(node as string);
                    }
                    foreach (var item in edgeList)
                    {
                        var edgeItem = item as ArrayList;
                        schemeObj.m_edgeDict[new EdgeKey(edgeItem[0] as string, edgeItem[1] as string)] =
                            edgeItem[2] as DataDict;
                    }
                    m_scheme[name] = schemeObj;
                }
                t1 = DateTime.Now;
                Logger.Debug("--------------AddScheme" + (t1 - t0).TotalMilliseconds.ToString());

                ReleaseLock();

                // code item
                var codeItemList = sceneData["codeItem"] as ArrayList;
                foreach (var item in codeItemList)
                {
                    var uname = item as string;
                    var entity = dbObj.SearchFromUniqueName(uname);
                    if (entity == null)
                    {
                        continue;
                    }
                    //AddCodeItem(item as string);
                    _DoAddCodeItem(item as string);
                }
                t1 = DateTime.Now;
                Logger.Debug("--------------AddCodeItem " + (t1 - t0).TotalMilliseconds.ToString());
                t0 = t1;

                // edge item
                var edgeItemList = sceneData["edgeItem"] as ArrayList;
                foreach (var edgePair in edgeItemList)
                {
                    var edgePairDict = edgePair as Dictionary<string, object>;
                    var edgePairList = edgePair as ArrayList;
                    var edgeKey = new EdgeKey(edgePairList[0] as string, edgePairList[1] as string);

                    bool isCustomEdge = false;
                    if (m_edgeDataDict.ContainsKey(edgeKey))
                    {
                        var edgeDataDict = m_edgeDataDict[edgeKey] as DataDict;
                        if (edgeDataDict.ContainsKey("customEdge") && (int)edgeDataDict["customEdge"] != 0)
                        {
                            _DoAddCodeEdgeItem(edgeKey.Item1, edgeKey.Item2, new DataDict { { "customEdge", 1 } });
                            isCustomEdge = true;
                        }
                    }
                    if (!isCustomEdge)
                    {
                        var refObj = dbObj.SearchRefObj(edgeKey.Item1, edgeKey.Item2);
                        if (refObj != null)
                        {
                            _DoAddCodeEdgeItem(edgeKey.Item1, edgeKey.Item2, new DataDict { { "dbRef", refObj } });
                        }
                        else
                        {
                            Logger.Debug("ignore edge:" + edgeKey.Item1 + " -> " + edgeKey.Item2);
                        }
                    }
                }
                t1 = DateTime.Now;
                Logger.Debug("--------------AddCodeEdgeItem " + (t1 - t0).TotalMilliseconds.ToString());
                Logger.Info("Open Time: " + (t1 - beginTime).TotalSeconds.ToString() + "s");
                t0 = t1;

                // Activeate commands
                mainUI.SetCommandActive(true);
                mainUI.UpdateUI();
                m_updateThread.ClearForceSleepTime();
                if (m_itemDict.Count == 0)
                {
                    Logger.Message("Analysis Result is opened.\n\nYou can place the cursor on a function/class/variable and use \"Show In Atlas\" command to show it on the viewport.");
                    mainUI.Dispatcher.BeginInvoke((ThreadStart)delegate {
                        mainUI.OnShowInAtlas(null, null);
                    });
                }
            });
            addingThread.Name = "Open DB Thread";
            addingThread.Start();
        }

        public void OnCloseDB()
        {
            // clear scene
            AcquireLock();
            SaveConfig();
            DeleteAllCodeItems();

            m_itemDict = new ItemDict();
            m_edgeDict = new EdgeDict();
            m_stopItem = new StopDict();
            m_itemDataDict = new Dictionary<string, DataDict>();
            m_edgeDataDict = new Dictionary<EdgeKey, DataDict>();
            m_scheme = new Dictionary<string, SchemeData>();
            m_curValidScheme = new List<string>();
            m_curValidSchemeColor = new List<Color>();

            ReleaseLock();
            
        }

        public void SaveConfig()
        {
            var dbPath = DBManager.Instance().GetDB().GetDBPath();
            if (dbPath == null || dbPath == "")
            {
                return;
            }

            var codeItemList = new List<string>();
            foreach (var item in m_itemDict)
            {
                codeItemList.Add(item.Key);
            }

            var edgeItemList = new List<List<string>>();
            foreach (var item in m_edgeDict)
            {
                edgeItemList.Add(new List<string> { item.Key.Item1, item.Key.Item2 });
            }

            var edgeDataList = new List<List<object>>();
            foreach (var item in m_edgeDataDict)
            {
                edgeDataList.Add(new List<object> { item.Key.Item1, item.Key.Item2, item.Value });
            }

            var scheme = new Dictionary<string, Dictionary<string, object>>();
            foreach (var schemeItem in m_scheme)
            {
                var schemeValue = schemeItem.Value;
                var schemeEdgeData = new List<List<object>>();
                foreach (var edgePair in schemeValue.m_edgeDict)
                {
                    schemeEdgeData.Add(new List<object> { edgePair.Key.Item1, edgePair.Key.Item2, edgePair.Value });
                }
                var schemeData = new Dictionary<string, object> {
                    { "node", schemeValue.m_nodeList },
                    { "edge", schemeEdgeData },
                };
                scheme[schemeItem.Key] = schemeData;
            }

            var jsonDict = new Dictionary<string, object> {
                {"stopItem", m_stopItem},
                {"codeItem", codeItemList },
                {"codeData", m_itemDataDict},
                {"edgeItem", edgeItemList },
                {"edgeData", edgeDataList },
                {"scheme", scheme }
            };

            try
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var jsonStr = js.Serialize(jsonDict);
                File.WriteAllText(dbPath + ".config", jsonStr);
            }
            catch (Exception)
            {
                Logger.Info("Save DB configuration failed.");
            }
        }
        #endregion

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

        public void SelectNothing()
        {
            _ClearSelection();
            m_selectTimeStamp += 1;
        }

        private void _ClearSelection()
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
            _ClearSelection();
            m_selectTimeStamp += 1;
            m_itemDict[uniqueName].IsSelected = true;
            return true;
        }

        public bool SelectOneItem(Shape item)
        {
            _ClearSelection();
            var node = item as CodeUIItem;
            var edge = item as CodeUIEdgeItem;
            if (node != null)
            {
                Logger.Debug("Select Node:" + node.GetUniqueName());
                m_selectTimeStamp += 1;
                node.IsSelected = true;
                return true;
            }
            else if (edge != null)
            {
                m_selectTimeStamp += 1;
                edge.IsSelected = true;
                return true;
            }
            return false;
        }

        public bool SelectOneEdge(CodeUIEdgeItem edge)
        {
            _ClearSelection();
            m_selectTimeStamp += 1;
            edge.IsSelected = true;
            return true;
        }

        public bool SelectEdges(List<EdgeKey> keys)
        {
            _ClearSelection();
            foreach (var key in keys)
            {
                if (m_edgeDict.ContainsKey(key))
                {
                    m_edgeDict[key].IsSelected = true;
                }
                m_selectTimeStamp += 1;
            }
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

            foreach (var item in itemList)
            {
                var uiItem = item as CodeUIItem;
                if (uiItem != null)
                {
                    uiItem.m_selectCounter += 1;
                    uiItem.m_selectTimeStamp = m_selectTimeStamp;
                    UpdateLRU(new List<string> { uiItem.GetUniqueName() });
                }

                var edgeItem = item as CodeUIEdgeItem;
                if (edgeItem != null)
                {
                    edgeItem.m_selectTimeStamp = m_selectTimeStamp;
                }
            }

            //RemoveItemLRU();

            // Update Comment
            var itemName = "";
            var itemComment = "";
            if (itemList.Count == 1)
            {
                var nodeItem = itemList[0] as CodeUIItem;
                var edgeItem = itemList[0] as CodeUIEdgeItem;
                if (nodeItem != null)
                {
                    itemName = nodeItem.GetName();
                    if (m_itemDataDict.ContainsKey(nodeItem.GetUniqueName()))
                    {
                        var dataDict = m_itemDataDict[nodeItem.GetUniqueName()];
                        if (dataDict.ContainsKey("comment"))
                        {
                            itemComment = (string)dataDict["comment"];
                        }
                    }
                }
                else if (edgeItem != null)
                {
                    if (m_itemDict.ContainsKey(edgeItem.m_srcUniqueName) &&
                        m_itemDict.ContainsKey(edgeItem.m_tarUniqueName))
                    {
                        var srcItem = m_itemDict[edgeItem.m_srcUniqueName];
                        var tarItem = m_itemDict[edgeItem.m_tarUniqueName];
                        itemName = srcItem.GetName() + " -> " + tarItem.GetName();
                        var edgeKey = new EdgeKey(edgeItem.m_srcUniqueName, edgeItem.m_tarUniqueName);
                        if (m_edgeDataDict.ContainsKey(edgeKey))
                        {
                            var dataDict = m_edgeDataDict[edgeKey];
                            if (dataDict.ContainsKey("comment"))
                            {
                                itemComment = (string)dataDict["comment"];
                            }
                        }
                    }
                }
            }
            var symbolWindow = UIManager.Instance().GetMainUI().GetSymbolWindow();
            if (symbolWindow != null)
            {
                symbolWindow.UpdateSymbol(itemName, itemComment);
            }
            // TODO: more code
            return true;
        }

        public void ShowInEditor()
        {
            var itemList = SelectedItems();
            if (itemList.Count == 0)
            {
                return;
            }

            var item = itemList[0];
            var navigator = new CursorNavigator();

            navigator.Navigate(item);
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

        public void FindNeighbour(Vector mainDirection, bool isInOrder = false)
        {
            // Logger.WriteLine("find neighbour:" + mainDirection.ToString());
            var t0 = DateTime.Now;
            var t1 = t0;
            var itemList = SelectedItems();
            if (itemList.Count == 0)
            {
                return;
            }
            AcquireLock();

            t1 = DateTime.Now;
            Logger.Debug("## AcquireLock " + (t1 - t0).TotalMilliseconds.ToString());
            t0 = t1;

            var centerItem = itemList[0];
            var centerNode = centerItem as CodeUIItem;
            var centerEdge = centerItem as CodeUIEdgeItem;
            Shape minItem = null;
            if (centerNode != null)
            {
                minItem = FindNeighbourForNode(centerNode, mainDirection, isInOrder);
            }
            else if (centerEdge != null)
            {
                minItem = FindNeighbourForEdge(centerEdge, mainDirection, isInOrder);
            }
            t1 = DateTime.Now;
            Logger.Debug("## Find Neighbour " + (t1 - t0).TotalMilliseconds.ToString());
            t0 = t1;
            
            if (minItem != null)
            {
                bool res = SelectOneItem(minItem);
                if (res)
                {
                    ShowInEditor();
                }
            }
            ReleaseLock();

            t1 = DateTime.Now;
            Logger.Debug("## ShowInEditor " + (t1 - t0).TotalMilliseconds.ToString());
            t0 = t1;
        }

        public Shape FindNeighbourForEdge(CodeUIEdgeItem centerItem, Vector mainDirection, bool isInOrder)
        {
            if (m_isSourceCandidate && centerItem.OrderData != null && Math.Abs(mainDirection.Y) > 0.8 && isInOrder)
            {
                var srcItem = m_itemDict[centerItem.m_srcUniqueName];
                var tarItem = m_itemDict[centerItem.m_tarUniqueName];
                if (srcItem != null && tarItem != null)// && 
                    //(srcItem.IsFunction()) &&
                    //(tarItem.IsFunction() || tarItem.IsVariable()))
                {
                    var tarOrder = centerItem.OrderData.m_order - 1;
                    if (mainDirection.Y > 0)
                    {
                        tarOrder = centerItem.OrderData.m_order + 1;
                    }
                    foreach (var edgePair in m_candidateEdge)
                    {
                        if (m_edgeDict.ContainsKey(edgePair))
                        {
                            var edge = m_edgeDict[edgePair];
                            if (edge.m_srcUniqueName == centerItem.m_srcUniqueName &&
                                edge.OrderData != null && edge.OrderData.m_order == tarOrder)
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

        public Shape FindNeighbourForNode(CodeUIItem centerItem, Vector mainDirection, bool isInOrder)
        {
            var centerPos = centerItem.Pos;
            var centerUniqueName = centerItem.GetUniqueName();

            //if (centerItem.IsFunction())
            {
                if (mainDirection.X > 0.8)
                {
                    // Find latest edge to jump to
                    CodeUIEdgeItem bestEdge = null;
                    int bestTimeStamp = 0;
                    CodeUIEdgeItem bestTimeStampEdge = null;
                    foreach (var item in m_edgeDict)
                    {
                        if (item.Key.Item1 == centerItem.GetUniqueName())
                        {
                            var edge = item.Value;
                            if(edge.m_selectTimeStamp > bestTimeStamp)
                            {
                                bestTimeStampEdge = edge;
                                bestTimeStamp = edge.m_selectTimeStamp;
                            }
                            if (edge.OrderData != null && edge.OrderData.m_order == 1 && isInOrder)
                            {
                                bestEdge = item.Value;
                            }
                        }
                    }
                    if (bestTimeStamp >= m_selectTimeStamp - 1 && bestTimeStampEdge != null)
                    {
                        return bestTimeStampEdge;
                    }
                    if (bestEdge != null)
                    {
                        return bestEdge;
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
            
            m_waitingItemMove = true;

            m_view.Dispatcher.BeginInvoke((ThreadStart)delegate
            {
                var now = System.Environment.TickCount;
                AcquireLock();
                m_itemMoveDistance = 0;
                foreach (var node in m_itemDict)
                {
                    var item = node.Value;
                    m_itemMoveDistance += item.MoveToTarget(0.3);
                }
                ReleaseLock();
                //Logger.Debug("MoveItems: BeginInvoke:" + (System.Environment.TickCount - now));
                m_waitingItemMove = false;
            }, DispatcherPriority.Send);
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
            // Logger.WriteLine("Add Code Item:" + srcUniqueName);
            if (m_itemDict.ContainsKey(srcUniqueName))
            {
                return false;
            }
            if (m_stopItem.ContainsKey(srcUniqueName))
            {
                return false;
            }

            var dbObj = DBManager.Instance().GetDB();
            var entity = dbObj.SearchFromUniqueName(srcUniqueName);
            if (entity == null)
            {
                return false;
            }

            // Build custom data
            var customData = new Dictionary<string, object>();
            customData["name"] = entity.Name();
            customData["longName"] = entity.Longname();
            customData["comment"] = GetComment(srcUniqueName);
            customData["kindName"] = entity.KindName();
            var metricRes = entity.Metric();
            customData["metric"] = metricRes;
            if (metricRes.ContainsKey("CountLine"))
            {
                var metricLine = metricRes["CountLine"].m_int;
                customData["lines"] = metricLine;
            }

            var kindStr = entity.KindName().ToLower();

            DoxygenDB.EntKind kind = DoxygenDB.EntKind.UNKNOWN;
            if (kindStr.Contains("function") || kindStr.Contains("method"))
            {
                kind = DoxygenDB.EntKind.FUNCTION;
                // Find caller and callee count
                var callerList = new List<DoxygenDB.Entity>();
                var callerRefList = new List<DoxygenDB.Reference>();
                var calleeList = new List<DoxygenDB.Entity>();
                var calleeRefList = new List<DoxygenDB.Reference>();
                dbObj.SearchRefEntity(out callerList, out callerRefList, srcUniqueName, "callby", "function, method", true);
                dbObj.SearchRefEntity(out calleeList, out calleeRefList, srcUniqueName, "call", "function, method", true);
                customData.Add("nCaller", callerList.Count);
                customData.Add("nCallee", calleeList.Count);
            }
            else if (kindStr.Contains("attribute") || kindStr.Contains("variable") ||
                kindStr.Contains("object"))
            {
                kind = DoxygenDB.EntKind.VARIABLE;
            }
            else if (kindStr.Contains("class") || kindStr.Contains("struct"))
            {
                kind = DoxygenDB.EntKind.CLASS;
            }
            else if (kindStr.Contains("file"))
            {
                kind = DoxygenDB.EntKind.FILE;
            }
            customData["kind"] = kind;

            customData["color"] = Color.FromRgb(195, 195, 195);
            if (kind == DoxygenDB.EntKind.FUNCTION || kind == DoxygenDB.EntKind.VARIABLE)
            {
                List<DoxygenDB.Entity> defineList;
                List<DoxygenDB.Reference> defineRefList;
                dbObj.SearchRefEntity(out defineList, out defineRefList, srcUniqueName, "definein");
                var name = "";
                var hasDefinition = true;
                if (defineList.Count == 0)
                {
                    dbObj.SearchRefEntity(out defineList, out defineRefList, srcUniqueName, "declarein");
                    hasDefinition = false;
                }
                customData.Add("hasDef", hasDefinition ? 1 : 0);
                if (defineList.Count != 0)
                {
                    foreach (var defineEnt in defineList)
                    {
                        if (defineEnt.KindName().ToLower().Contains("class") ||
                            defineEnt.KindName().ToLower().Contains("struct"))
                        {
                            name = defineEnt.Name();
                            customData["className"] = name;
                            customData["color"] = CodeUIItem.NameToColor(name);
                            break;
                        }
                    }
                }
            }
            else if (kind == DoxygenDB.EntKind.CLASS)
            {
                customData["color"] = CodeUIItem.NameToColor((string)customData["name"]);
            }

            // Add CodeUIItem
            this.Dispatcher.Invoke((ThreadStart)delegate
            {
                AcquireLock();
                var item = new CodeUIItem(srcUniqueName, customData);
                m_itemDict[srcUniqueName] = item;
                m_view.canvas.Children.Add(item);
                Point center;
                GetSelectedCenter(out center);
                item.Pos = center;
                item.SetTargetPos(center);
                m_isLayoutDirty = true;
                if (m_itemDict.Count == 1)
                {
                    SelectOneItem(item);
                }
                ReleaseLock();
            });
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

        bool _DoAddCodeEdgeItem(string srcUniqueName, string tarUniqueName, DataDict data)
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

            if (srcUniqueName == tarUniqueName)
            {
                return false;
            }

            // Add CodeUIItem
            this.Dispatcher.Invoke((ThreadStart)delegate
            {
                AcquireLock();
                var srcNode = m_itemDict[srcUniqueName];
                var tarNode = m_itemDict[tarUniqueName];
                var edgeItem = new CodeUIEdgeItem(srcUniqueName, tarUniqueName, data);
                m_edgeDict.Add(key, edgeItem);
                if (data != null && data.ContainsKey("customEdge"))
                {
                    bool isCustomEdge = (int)data["customEdge"] != 0;
                    if (isCustomEdge)
                    {
                        if (!m_edgeDataDict.ContainsKey(key))
                        {
                            m_edgeDataDict.Add(key, new DataDict { { "customEdge", 1 } });
                        }
                        else
                        {
                            AddOrReplaceDict(m_edgeDataDict[key], "customEdge", 1);
                        }
                    }
                }
                m_view.canvas.Children.Add(edgeItem);
                m_isLayoutDirty = true;
                ReleaseLock();
            });

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

        public void AddSimilarCodeItem()
        {
            var itemList = SelectedItems();
            if (itemList.Count == 0)
            {
                return;
            }

            var item = itemList[0] as CodeUIItem;
            if (item == null)
            {
                return;
            }

            var db = DBManager.Instance().GetDB();
            var name = item.GetName();
            var uname = item.GetUniqueName();
            if (db == null || name == null || name == "" || item.GetKind() != DoxygenDB.EntKind.FUNCTION)
            {
                return;
            }

            var ents = db.Search(name, "function");
            if (ents.Count == 0)
            {
                return;
            }
            var bestEntList = new List<DoxygenDB.Entity>();
            foreach (var ent in ents)
            {
                if (ent.Name() == name)
                {
                    bestEntList.Add(ent);
                }
            }

            foreach (var ent in bestEntList)
            {
                var entUname = ent.UniqueName();
                if (uname == entUname)
                {
                    continue;
                }
                AddCodeItem(entUname);
                bool hasEdge = false;
                foreach (var edgePair in m_edgeDict)
                {
                    var edgeKey = edgePair.Key;
                    if (edgeKey.Item1 == uname && edgeKey.Item2 == entUname)
                    {
                        hasEdge = true;
                        break;
                    }
                    if (edgeKey.Item2 == uname && edgeKey.Item1 == entUname)
                    {
                        hasEdge = true;
                        break;
                    }
                }
                if (hasEdge || !m_itemDict.ContainsKey(entUname))
                {
                    continue;
                }

                var entItem = m_itemDict[entUname];
                var customData = entItem.GetCustomData("hasDef");
                if (customData != null && customData.m_int == 0)
                {
                    DoAddCustomEdge(entUname, uname);
                }
                else
                {
                    DoAddCustomEdge(uname, entUname);
                }
            }
        }

        public bool DoAddCustomEdge(string srcName, string tarName, DataDict edgeData = null)
        {
            if (!m_itemDict.ContainsKey(srcName) || !m_itemDict.ContainsKey(tarName) || m_edgeDict.ContainsKey(new EdgeKey(srcName, tarName)))
            {
                return false;
            }
            if (edgeData == null)
            {
                edgeData = new DataDict();
            }
            edgeData["customEdge"] = 1;

            AcquireLock();
            _DoAddCodeEdgeItem(srcName, tarName, edgeData);
            ReleaseLock();
            return true;
        }

        public bool BeginAddCustomEdge()
        {
            AcquireLock();
            var selectedNodes = SelectedNodes();
            if (selectedNodes.Count == 1)
            {
                // Clear other items
                string oldSource = "";
                foreach (var item in m_itemDict)
                {
                    if (item.Value.GetCustomEdgeSourceMode())
                    {
                        oldSource = item.Value.GetUniqueName();
                    }
                    item.Value.SetCustomEdgeSourceMode(false);
                }
                // Toggle selected node as custom edge source
                var srcNode = selectedNodes[0];
                if (oldSource == srcNode.GetUniqueName())
                {
                    srcNode.SetCustomEdgeSourceMode(false);
                    m_customEdgeSource = "";
                }
                else
                {
                    srcNode.SetCustomEdgeSourceMode(true);
                    m_customEdgeSource = srcNode.GetUniqueName();
                }
            }
            ReleaseLock();
            return true;
        }

        public bool EndAddCustomEdge()
        {
            AcquireLock();
            if (m_itemDict.ContainsKey(m_customEdgeSource))
            {
                var selectedNodes = SelectedNodes();
                foreach (var item in selectedNodes)
                {
                    DoAddCustomEdge(m_customEdgeSource, item.GetUniqueName());
                }
                var srcItem = m_itemDict[m_customEdgeSource];
                srcItem.SetCustomEdgeSourceMode(false);

                var edgesToSelect = new List<EdgeKey>();
                foreach (var item in selectedNodes)
                {
                    edgesToSelect.Add(new EdgeKey(m_customEdgeSource, item.GetUniqueName()));
                }
                SelectEdges(edgesToSelect);
                m_customEdgeSource = "";
            }
            ReleaseLock();
            return true;
        }

        public void DeleteCodeItem(string uniqueName)
        {
            AcquireLock();
            _DoDeleteCodeItem(uniqueName);
            DeleteLRU(new List<string> { uniqueName });
            RemoveItemLRU();
            ReleaseLock();
        }

        public void DeleteAllCodeItems()
        {
            AcquireLock();
            var unameList = m_itemDict.Keys.ToList<string>();
            foreach (var uname in unameList)
            {
                _DoDeleteCodeItem(uname);
            }
            DeleteLRU(unameList);
            ReleaseLock();
        }

        public void DeleteNearbyItems()
        {
            var minScoreList = new List<Tuple<int, string>>();
            AcquireLock();
            foreach (var item in m_edgeDict)
            {
                var srcItem = m_itemDict[item.Key.Item1];
                var tarItem = m_itemDict[item.Key.Item2];
                if (srcItem.IsSelected == tarItem.IsSelected)
                {
                    continue;
                }
                if (srcItem.IsSelected)
                {
                    minScoreList.Add(new Tuple<int, string>(tarItem.m_selectCounter, tarItem.GetUniqueName()));
                }
                else if(tarItem.IsSelected)
                {
                    minScoreList.Add(new Tuple<int, string>(srcItem.m_selectCounter, srcItem.GetUniqueName()));
                }
            }

            if (minScoreList.Count > 0)
            {
                minScoreList.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                var deleteScore = minScoreList[0].Item1;
                var deleteList = new List<string>();
                foreach (var item in minScoreList)
                {
                    if (item.Item1 == deleteScore)
                    {
                        deleteList.Add(item.Item2);
                    }
                }

                foreach (var item in deleteList)
                {
                    _DoDeleteCodeItem(item);
                }

                DeleteLRU(deleteList);
            }

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
                        m_stopItem.Add(item.Key, item.Value.GetName());
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

            bool res = false;
            if (lastFunction != null)
            {
                res = SelectOneEdge(lastFunction);
            }
            if (res == false && !double.IsNaN(lastPos.X))
            {
                res = SelectNearestItem(lastPos);
            }

            if (res)
            {
                ShowInEditor();
            }
            ReleaseLock();
        }
        #endregion

        #region Forbidden Symbols
        public void AddForbiddenSymbol()
        {
            foreach (var item in m_itemDict)
            {
                var node = item.Value;
                if (node.IsSelected)
                {
                    m_stopItem[node.GetUniqueName()] = node.GetName();
                }
            }
        }

        public Dictionary<string, string> GetForbiddenSymbol()
        {
            return m_stopItem;
        }

        public void DeleteForbiddenSymbol(string uname)
        {
            if (m_stopItem.ContainsKey(uname))
            {
                m_stopItem.Remove(uname);
            }
        }
        #endregion

        #region Comment
        public string GetComment(string id)
        {
            // TODO: Add code
            if (m_itemDataDict.ContainsKey(id))
            {
                var dataDict = m_itemDataDict[id];
                if (dataDict.ContainsKey("comment"))
                {
                    return (string)dataDict["comment"];
                }
            }
            return "";
        }

        public void UpdateSelectedComment(string comment)
        {
            var itemList = SelectedItems();
            AcquireLock();
            if (itemList.Count == 1)
            {
                var item = itemList[0];
                var nodeItem = item as CodeUIItem;
                var edgeItem = item as CodeUIEdgeItem;
                if (nodeItem != null)
                {
                    DataDict itemData;
                    m_itemDataDict.TryGetValue(nodeItem.GetUniqueName(), out itemData);
                    if (itemData == null)
                    {
                        itemData = new DataDict();
                        m_itemDataDict[nodeItem.GetUniqueName()] = itemData;
                    }
                    AddOrReplaceDict(itemData,"comment",comment);
                    nodeItem.UpdateComment(comment);
                }
                else if (edgeItem != null)
                {
                    var srcItem = m_itemDict[edgeItem.m_srcUniqueName];
                    var tarItem = m_itemDict[edgeItem.m_tarUniqueName];
                    if (srcItem != null && tarItem != null)
                    {
                        var edgeKey = new EdgeKey(edgeItem.m_srcUniqueName, edgeItem.m_tarUniqueName);
                        DataDict edgeData;
                        m_edgeDataDict.TryGetValue(edgeKey, out edgeData);
                        if (edgeData == null)
                        {
                            edgeData = new DataDict();
                            m_edgeDataDict[edgeKey] = edgeData;
                        }
                        AddOrReplaceDict(edgeData,"comment", comment);
                    }
                }

                m_isLayoutDirty = true;
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
                        _DoAddCodeEdgeItem(uniqueName, canEntName, new DataDict { { "dbRef", canRefObj } });
                    }
                    else
                    {
                        _DoAddCodeEdgeItem(canEntName, uniqueName, new DataDict { { "dbRef", canRefObj } });
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
            //if (res)
            //{
            //    SelectNearestItem(center);
            //}
            ReleaseLock();
        }
        #endregion

        #region Scheme
        public void AddOrReplaceIthScheme(int ithScheme)
        {
            if (ithScheme < 0 || ithScheme >= m_curValidScheme.Count)
            {
                return;
            }
            var name = m_curValidScheme[ithScheme];
            AddOrReplaceScheme(name);
            ShowScheme(name, true);
            m_schemeTimeStamp++;
        }

        public void ToggleSelectedEdgeToScheme(int ithScheme)
        {
            if (ithScheme < 0 || ithScheme >= m_curValidScheme.Count)
            {
                return;
            }

            AcquireLock();
            var name = m_curValidScheme[ithScheme];
            var schemeData = m_scheme[name];
            var schemeNodeSet = new HashSet<string>(schemeData.m_nodeList);
            var schemeEdgeDict = schemeData.m_edgeDict;

            foreach (var sceneEdgePair in m_edgeDict)
            {
                var edgeName = sceneEdgePair.Key;
                var edge = sceneEdgePair.Value;
                if (edge.IsSelected)
                {
                    bool isAdd = true;
                    if (schemeEdgeDict.ContainsKey(edgeName))
                    {
                        isAdd = false;
                    }

                    if (isAdd)
                    {
                        schemeEdgeDict[edgeName] = new DataDict();
                        schemeNodeSet.Add(edge.m_srcUniqueName);
                        schemeNodeSet.Add(edge.m_tarUniqueName);
                    }
                    else
                    {
                        schemeEdgeDict.Remove(edgeName);
                        var isSrcNodeDelete = schemeNodeSet.Contains(edge.m_srcUniqueName);
                        var isTarNodeDelete = schemeNodeSet.Contains(edge.m_tarUniqueName);
                        foreach (var schemeEdgePair in schemeEdgeDict)
                        {
                            var edgePair = schemeEdgePair.Key;
                            var edgeData = schemeEdgePair.Value;
                            if (edge.m_srcUniqueName == edgePair.Item1 || edge.m_srcUniqueName == edgePair.Item2)
                            {
                                isSrcNodeDelete = false;
                            }
                            if (edge.m_tarUniqueName == edgePair.Item1 || edge.m_tarUniqueName == edgePair.Item2)
                            {
                                isTarNodeDelete = false;
                            }
                        }

                        if (isSrcNodeDelete)
                        {
                            schemeNodeSet.Remove(edge.m_srcUniqueName);
                        }
                        if (isTarNodeDelete)
                        {
                            schemeNodeSet.Remove(edge.m_tarUniqueName);
                        }
                    }
                }
            }
            schemeData.m_nodeList = new List<string>(schemeNodeSet);
            m_schemeTimeStamp++;
            ReleaseLock();
        }

        public void AddOrReplaceScheme(string name)
        {
            AcquireLock();
            var nodes = new List<string>();
            foreach (var item in m_itemDict)
            {
                if (item.Value.IsSelected)
                {
                    nodes.Add(item.Value.GetUniqueName());
                }
            }

            var scheme = new SchemeData();
            scheme.m_nodeList = nodes;
            foreach (var itemPair in m_edgeDict)
            {
                var edgePair = itemPair.Key;
                var item = itemPair.Value;
                if (m_itemDict.ContainsKey(item.m_srcUniqueName) &&
                    m_itemDict.ContainsKey(item.m_tarUniqueName))
                {
                    var srcItem = m_itemDict[item.m_srcUniqueName];
                    var tarItem = m_itemDict[item.m_tarUniqueName];
                    
                    if (item.IsSelected)
                    {
                        if (!srcItem.IsSelected)
                        {
                            scheme.m_nodeList.Add(srcItem.GetUniqueName());
                        }
                        if (!tarItem.IsSelected)
                        {
                            scheme.m_nodeList.Add(tarItem.GetUniqueName());
                        }
                        scheme.m_edgeDict.Add(edgePair, new DataDict());
                    }
                    else if (srcItem.IsSelected && tarItem.IsSelected)
                    {
                        scheme.m_edgeDict.Add(edgePair, new DataDict());
                    }
                }
            }

            if (!scheme.IsEmpty())
            {
                m_scheme[name] = scheme;
                m_schemeTimeStamp++;
            }

            ReleaseLock();
        }

        public List<string> GetSchemeNameList()
        {
            List<string> nameList = new List<string>();
            foreach (var item in m_scheme)
            {
                nameList.Add(item.Key);
            }
            return nameList;
        }

        public void DeleteScheme(string name)
        {
            if (m_scheme.ContainsKey(name))
            {
                m_scheme.Remove(name);
            }
            m_schemeTimeStamp++;
        }

        public void ShowScheme(string name, bool selectScheme = true)
        {
            if (!m_scheme.ContainsKey(name))
            {
                return;
            }

            AcquireLock();
            var selectedNode = new List<string>(); 
            var selectedEdge = new List<EdgeKey>();
            if (selectScheme == false)
            {
                foreach (var item in m_itemDict)
                {
                    if (item.Value.IsSelected)
                    {
                        selectedNode.Add(item.Key);
                    }
                }
                foreach (var item in m_edgeDict)
                {
                    if (item.Value.IsSelected)
                    {
                        selectedEdge.Add(item.Key);
                    }
                }
            }

            var scheme = m_scheme[name];
            var codeItemList = scheme.m_nodeList;
            foreach (var uname in codeItemList)
            {
                AddCodeItem(uname);
            }

            _ClearSelection();
            foreach (var uname in codeItemList)
            {
                if (!m_itemDict.ContainsKey(uname))
                {
                    continue;
                }
                var item = m_itemDict[uname];
                if (selectScheme)
                {
                    item.IsSelected = true;
                }
            }

            var edgeItemDict = scheme.m_edgeDict;
            var dbObj = DBManager.Instance().GetDB();
            foreach (var edgeItem in edgeItemDict)
            {
                var edgePair = edgeItem.Key;
                var edgeData = new DataDict();
                if (m_edgeDataDict.ContainsKey(edgePair))
                {
                    edgeData = m_edgeDataDict[edgePair];
                }
                bool customEdge = false;
                if (edgeData.ContainsKey("customEdge"))
                {
                    customEdge = (int)edgeData["customEdge"] != 0;
                }

                if (customEdge)
                {
                    _DoAddCodeEdgeItem(edgePair.Item1, edgePair.Item2, new DataDict { { "customEdge", 1} });
                }
                else
                {
                    var refObj = dbObj.SearchRefObj(edgePair.Item1, edgePair.Item2);
                    if (refObj != null)
                    {
                        _DoAddCodeEdgeItem(edgePair.Item1, edgePair.Item2, new DataDict { { "dbRef", refObj } });
                    }
                }
                if (m_edgeDict.ContainsKey(edgePair) && selectScheme)
                {
                    m_edgeDict[edgePair].IsSelected = true;
                }
            }

            if (!selectScheme)
            {
                foreach (var uname in selectedNode)
                {
                    if (m_itemDict.ContainsKey(uname))
                    {
                        m_itemDict[uname].IsSelected = true;
                    }
                }
                foreach (var uname in selectedEdge)
                {
                    if (m_edgeDict.ContainsKey(uname))
                    {
                        m_edgeDict[uname].IsSelected = true;
                    }
                }
            }
            m_schemeTimeStamp++;
            ReleaseLock();
        }

        public void ShowIthScheme(int ithScheme, bool isSelected = false)
        {
            if (ithScheme < 0 || ithScheme >= m_curValidScheme.Count)
            {
                return;
            }

            var name = m_curValidScheme[ithScheme];
            ShowScheme(name, isSelected);
        }

        public List<string> GetCurrentSchemeList()
        {
            return m_curValidScheme;
        }

        public List<Color> GetCurrentSchemeColorList()
        {
            return m_curValidSchemeColor;
        }

        public void UpdateCurrentValidScheme()
        {
            var schemeNameSet = new HashSet<string>();

            var edgeSet = new HashSet<EdgeKey>();
            var nodeSet = new HashSet<string>();
            var isFadingMap = new Dictionary<string, bool>();
            foreach (var item in m_itemDict)
            {
                if (item.Value.IsSelected)
                {
                    nodeSet.Add(item.Key);
                }
                isFadingMap[item.Key] = false;
            }
            foreach (var edgePair in m_edgeDict)
            {
                var uname = edgePair.Key;
                var item = edgePair.Value;
                item.ClearSchemeColorList();
                if (item.IsSelected)
                {
                    edgeSet.Add(uname);
                    nodeSet.Add(item.m_srcUniqueName);
                    nodeSet.Add(item.m_tarUniqueName);
                }
                else if (m_itemDict[item.m_srcUniqueName].IsSelected)
                {
                    edgeSet.Add(uname);
                    nodeSet.Add(item.m_srcUniqueName);
                }
                else if (m_itemDict[item.m_tarUniqueName].IsSelected)
                {
                    edgeSet.Add(uname);
                    nodeSet.Add(item.m_tarUniqueName);
                }
            }

            foreach (var uname in nodeSet)
            {
                foreach (var item in m_scheme)
                {
                    if (item.Value.m_nodeList.Contains(uname))
                    {
                        schemeNameSet.Add(item.Key);
                    }
                }
            }

            foreach (var uname in edgeSet)
            {
                foreach (var item in m_scheme)
                {
                    var schemeName = item.Key;
                    var schemeData = item.Value;
                    if (schemeData.m_edgeDict.ContainsKey(uname))
                    {
                        schemeNameSet.Add(schemeName);
                    }
                }
            }

            // If no scheme, no item is fading
            if (schemeNameSet.Count != 0)
            {
                foreach (var item in m_itemDict)
                {
                    isFadingMap[item.Key] = true;
                }
            }

            m_curValidScheme = schemeNameSet.ToList();
            m_curValidScheme.Sort((x, y) => x.CompareTo(y));
            m_curValidSchemeColor.Clear();

            foreach (var schemeName in m_curValidScheme)
            {
                var schemeData = m_scheme[schemeName];
                var schemeColor = CodeUIItem.NameToColor(schemeName);
                m_curValidSchemeColor.Add(schemeColor);
                foreach (var item in schemeData.m_edgeDict)
                {
                    var edgePair = item.Key;
                    var edgeData = item.Value;
                    if (m_edgeDict.ContainsKey(edgePair))
                    {
                        var edge = m_edgeDict[edgePair];
                        edge.AddSchemeColor(schemeColor);
                    }
                }
                foreach (var item in schemeData.m_nodeList)
                {
                    if (m_itemDict.ContainsKey(item))
                    {
                        isFadingMap[item] = false;
                    }
                }
            }

            foreach (var fadePair in isFadingMap)
            {
                m_itemDict[fadePair.Key].IsFading = fadePair.Value;
            }
            if (m_view != null)
            {
                m_view.InvalidateScheme();
            }
            
        }
        #endregion
        public void Invalidate()
        {
            AcquireLock();
            m_isInvalidate = true;
            ReleaseLock();
        }

        public void ClearInvalidate()
        {
            if (m_isInvalidate)
            {
                bool isInvalidating = false;
                AcquireLock();
                foreach (var node in m_itemDict)
                {
                    if (node.Value.IsInvalidating())
                    {
                        isInvalidating = true;
                        break;
                    }
                }
                foreach (var item in m_edgeDict)
                {
                    if (item.Value.IsInvalidating())
                    {
                        isInvalidating = true;
                        break;
                    }
                }
                ReleaseLock();
                if (isInvalidating)
                {
                    return;
                }

                AcquireLock();
                foreach (var node in m_itemDict)
                {
                    node.Value.Invalidate();
                }
                ReleaseLock();

                AcquireLock();
                foreach (var edge in m_edgeDict)
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
                m_isInvalidate = false;
                ReleaseLock();
            }
        }
    }
}
