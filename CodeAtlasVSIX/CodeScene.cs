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

    public class CodeScene
    {
        ItemDict m_itemDict = new ItemDict();
        EdgeDict m_edgeDict = new EdgeDict();
        CodeView m_view = null;

        public CodeScene()
        {

        }

        public void SetView(CodeView view)
        {
            m_view = view;
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

            var edgeItem = new CodeUIEdgeItem(srcUniqueName, tarUniqueName);
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
