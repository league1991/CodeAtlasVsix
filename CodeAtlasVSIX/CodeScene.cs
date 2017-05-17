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
    using System.Threading;
    using System.Windows.Data;
    using System.Windows.Controls;
    using System.Windows;

    public class CodeScene
    {
        ItemDict m_itemDict = new ItemDict();
        EdgeDict m_edgeDict = new EdgeDict();
        CodeView m_view = null;
        SceneUpdateThread m_updateThread = null;
        object m_lockObj = new object();
        public bool m_isLayoutDirty = false;

        List<string> m_itemLruQueue = new List<string>();
        int m_lruMaxLength = 20;

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

        public void OnOpenDB()
        {
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
        #endregion

        public void MoveItems()
        {
            if(m_view == null)
            {
                return;
            }
            m_view.Dispatcher.Invoke((ThreadStart)delegate
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
            if(m_itemLruQueue.Count > m_lruMaxLength)
            {
                for(int i = m_lruMaxLength; i < m_itemLruQueue.Count; ++i)
                {
                    _DoDeleteCodeItem(m_itemLruQueue[m_lruMaxLength]);
                    m_itemLruQueue.RemoveAt(m_lruMaxLength);
                }
            }
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
        }

        void _DoDeleteCodeEdgeItem(EdgeKey edgeKey)
        {
            if (!m_edgeDict.ContainsKey(edgeKey))
            {
                return;
            }

            m_view.canvas.Children.Remove(m_edgeDict[edgeKey]);
            m_edgeDict.Remove(edgeKey);
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
            return true;
        }

        public void AddCodeItem(string srcUniqueName)
        {
            AcquireLock();
            _DoAddCodeItem(srcUniqueName);
            UpdateLRU(new List<string> { srcUniqueName});
            RemoveItemLRU();
            m_isLayoutDirty = true;
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
            m_isLayoutDirty = true;
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
