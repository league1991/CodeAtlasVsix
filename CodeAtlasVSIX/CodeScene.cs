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

    public class CodeScene
    {
        ItemDict m_itemDict = new ItemDict();
        EdgeDict m_edgeDict = new EdgeDict();
        CodeView m_view = null;
        SceneUpdateThread m_updateThread = null;

        public CodeScene()
        {
            m_updateThread = new SceneUpdateThread(this);
            m_updateThread.Start();
        }

        public void UpdateShape()
        {
            foreach(var item in m_itemDict)
            {
                item.Value.UpdateShape();
            }

            foreach(var edge in m_edgeDict)
            {
                edge.Value.UpdateShape();
            }
        }

        public void SetView(CodeView view)
        {
            m_view = view;
        }

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

        bool _DoAddCodeItem(string srcUniqueName)
        {
            if (m_itemDict.ContainsKey(srcUniqueName))
            {
                return false;
            }
            var item = new CodeUIItem();
            m_itemDict[srcUniqueName] = item;
            m_view.canvas.Children.Add(item);
            return true;
        }

        bool _DoAddCodeEdgeItem(string srcUniqueName, string tarUniqueName, object data = null)
        {
            var key = new EdgeKey(srcUniqueName, tarUniqueName);
            if(m_edgeDict.ContainsKey(key))
            {
                return false;
            }

            var srcNode = m_itemDict[srcUniqueName];
            var tarNode = m_itemDict[tarUniqueName];
            var edgeItem = new CodeUIEdgeItem(srcUniqueName, tarUniqueName);
            var srcBinding = new Binding("RightPoint") { Source = srcNode };
            var tarBinding = new Binding("LeftPoint") { Source = tarNode };
            BindingOperations.SetBinding(edgeItem, CodeUIEdgeItem.StartPointProperty, srcBinding);
            BindingOperations.SetBinding(edgeItem, CodeUIEdgeItem.EndPointProperty, tarBinding);
            m_edgeDict.Add(key, edgeItem);
            m_view.canvas.Children.Add(edgeItem);
            return true;
        }

        public bool AddCodeItem(string srcUniqueName)
        {
            return _DoAddCodeItem(srcUniqueName);
        }

        public bool AddCodeEdgeItem(string srcUniqueName, string tarUniqueName)
        {
            return _DoAddCodeEdgeItem(srcUniqueName, tarUniqueName);
        }
    }
}
