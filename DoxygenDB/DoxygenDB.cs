using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace DoxygenDB
{
    class IndexItem
    {
        public enum Kind
        {
            UNKNOWN = 0,
            // compound,
            CLASS = 1,
            STRUCT = 2,
            UNION = 3,
            INTERFACE = 4,
            PROTOCOL = 5,
            CATEGORY = 6,
            EXCEPTION = 7,
            FILE = 8,
            NAMESPACE = 9,
            GROUP = 10,
            PAGE = 11,
            EXAMPLE = 12,
            // member,
            DIR = 13,
            DEFINE = 14,
            PROPERTY = 15,
            EVENT = 16,
            VARIABLE = 17,
            TYPEDEF = 18,
            ENUM = 19,
            ENUMVALUE = 20,
            FUNCTION = 21,
            SIGNAL = 22,
            PROTOTYPE = 23,
            FRIEND = 24,
            DCOP = 25,
            SLOT = 26
        }

        public static Dictionary<string, Kind> s_kindDict = new Dictionary<string, Kind>{
            {"unknown", Kind.UNKNOWN},      {"class", Kind.CLASS},          {"struct", Kind.STRUCT},
            {"union", Kind.UNION},          {"interface", Kind.INTERFACE},  {"protocol", Kind.PROTOCOL},
            {"category", Kind.CATEGORY},    {"exception", Kind.EXCEPTION},  {"file", Kind.FILE},
            {"namespace", Kind.NAMESPACE},  {"group", Kind.GROUP},          {"page", Kind.PAGE},
            {"example", Kind.EXAMPLE},      {"dir", Kind.DIR},              {"define", Kind.DEFINE},
            {"property", Kind.PROPERTY},    {"event", Kind.EVENT},          {"variable", Kind.VARIABLE},
            {"typedef", Kind.TYPEDEF},      {"enum", Kind.ENUM},            {"enumvalue", Kind.ENUMVALUE},
            {"function", Kind.FUNCTION},    {"signal", Kind.SIGNAL},        {"prototype", Kind.PROTOTYPE},
            {"friend", Kind.FRIEND},        {"dcop", Kind.DCOP},            {"slot", Kind.SLOT},
            // extra keywords
            {"method", Kind.FUNCTION}
        };

        public string m_id;
        public string m_name;
        public Kind m_kind;
        public List<IndexRefItem> m_refs = new List<IndexRefItem>();

        public IndexItem(string name, string kindStr, string id)
        {
            m_name = name;
            m_id = id;
            m_kind = s_kindDict[kindStr];
        }

        public bool IsCompoundKind()
        {
            return (int)m_kind >= 1 && (int)m_kind <= 13;
        }

        public bool IsMemberKind()
        {
            return (int)m_kind >= 14 && (int)m_kind <= 26;
        }

        public void AddRefItem(IndexRefItem refItem)
        {
            m_refs.Add(refItem);
        }

        public List<IndexRefItem> GetRefItemList()
        {
            return m_refs;
        }
    }

    class IndexRefItem
    {
        public enum Kind
        {
            UNKNOWN = 0,
            MEMBER = 1,
            CALL = 2,
            DERIVE = 3,
            USE = 4,
            OVERRIDE = 5,
            DECLARE = 6,
            DEFINE = 7,
        }
        
        public static Dictionary<string, Tuple<Kind, bool>> s_kindDict = new Dictionary<string, Tuple<Kind, bool>>
        {
            {"reference"       , new Tuple<Kind, bool>(Kind.UNKNOWN,  false)},
            {"unknown"         , new Tuple<Kind, bool>(Kind.UNKNOWN,  false)},
            {"call"            , new Tuple<Kind, bool>(Kind.CALL,     false)},
            {"callby"          , new Tuple<Kind, bool>(Kind.CALL,     true)},
            {"base"            , new Tuple<Kind, bool>(Kind.DERIVE,   true)},
            {"derive"          , new Tuple<Kind, bool>(Kind.DERIVE,   false)},
            {"use"             , new Tuple<Kind, bool>(Kind.USE,      false)},
            {"useby"           , new Tuple<Kind, bool>(Kind.USE,      true)},
            {"member"          , new Tuple<Kind, bool>(Kind.MEMBER,   false)},
            {"declare"         , new Tuple<Kind, bool>(Kind.DECLARE,  false)},
            {"define"          , new Tuple<Kind, bool>(Kind.DEFINE,   false)},
            {"declarein"       , new Tuple<Kind, bool>(Kind.DECLARE,  true)},
            {"definein"        , new Tuple<Kind, bool>(Kind.DEFINE,   true)},
            {"override"        , new Tuple<Kind, bool>(Kind.OVERRIDE, true)},
            {"overrides"       , new Tuple<Kind, bool>(Kind.OVERRIDE, true)},
            {"overriddenby"    , new Tuple<Kind, bool>(Kind.OVERRIDE, false)},
        };

        public string m_srcId;
        public string m_dstId;
        public Kind m_kind;
        public string m_file;
        int m_line;
        int m_column;

        public IndexRefItem(string srcId, string dstId, string refKindStr, string file = "", int line = 0, int column = 0)
        {
            m_srcId = srcId;
            m_dstId = dstId;
            m_kind = s_kindDict[refKindStr].Item1;
            m_file = file;
            m_line = line;
            m_column = column;
        }
    }

    public class XmlDocItem
    {
        public enum CacheStatus
        {
            NONE = 0,
            REF  = 1
        }

        public XPathDocument m_doc;
        public XPathNavigator m_navigator;
        public int m_cacheStatus;

        public XmlDocItem(XPathDocument doc)
        {
            m_doc = doc;
            m_navigator = doc.CreateNavigator();
            m_cacheStatus = (int)CacheStatus.NONE;
        }

        public XPathDocument GetDoc()
        {
            return m_doc;
        }

        public bool GetCacheStatus(CacheStatus status)
        {
            return (m_cacheStatus & (int)status) > 0;
        }

        public void SetCacheStatus(CacheStatus status)
        {
            m_cacheStatus |= (int)status;
        }
    }

    class Entity
    {
        public string m_id;
        public string m_shortName;
        public string m_longName;
        public string m_kindName;
        public Dictionary<string, object> m_metric = new Dictionary<string, object>();

        public Entity(string id, string name, string longName, string kindName, Dictionary<string, object> metric)
        {
            m_id = id;
            m_shortName = name;
            m_longName = longName;
            m_kindName = kindName;
            m_metric = metric;
        }

        public string Name()
        {
            return m_shortName;
        }

        public string Longname()
        {
            return m_longName;
        }

        public string UniqueName()
        {
            return m_id;
        }

        public string KindName()
        {
            return m_kindName;
        }

        public object Metric()
        {
            return null;
        }
    }

    class Reference
    {
        public IndexRefItem.Kind m_kind;
        public string m_entityId;
        public Entity m_entity;
        public string m_fileName;
        public int m_lineNum;
        public int m_columnNum;

        public Reference(IndexRefItem.Kind kind, Entity entity, string file, int line, int column)
        {
            m_kind = kind;
            m_entityId = entity.m_id;
            m_entity = entity;
            m_fileName = file;
            m_lineNum = line;
            m_columnNum = column;
        }

        public Entity File()
        {
            return new Entity("", m_fileName, m_fileName, "file", new Dictionary<string, object>());
        }

        public int Line()
        {
            return m_lineNum;
        }

        public int Column()
        {
            return m_columnNum;
        }
    }

    class DoxygenDB
    {
        string m_dbFolder;
        Dictionary<string, string> m_idToCompoundDict = new Dictionary<string, string>();
        Dictionary<string, List<string>> m_compoundToIdDict = new Dictionary<string, List<string>>();
        Dictionary<string, IndexItem> m_idInfoDict = new Dictionary<string, IndexItem>();
        Dictionary<string, XmlDocItem> m_xmlCache = new Dictionary<string, XmlDocItem>();
        Dictionary<string, XPathNavigator> m_xmlElementCache = new Dictionary<string, XPathNavigator>();

        XPathNavigator _GetXmlDocument(string fileName)
        {
            return _GetXmlDocumentItem(fileName).m_navigator;
        }

        XmlDocItem _GetXmlDocumentItem(string fileName)
        {
            var filePath = string.Format("{0}/{1}.xml", m_dbFolder, fileName);
            if(m_xmlCache.ContainsKey(filePath))
            {
                return m_xmlCache[filePath];
            }

            var xmlDoc = new XPathDocument(filePath);
            var xmlDocItem = new XmlDocItem(xmlDoc);
            m_xmlCache[filePath] = xmlDocItem;
            return xmlDocItem;
        }

        string _GetCompoundPath(string compoundId)
        {
            if (compoundId == null)
            {
                return "";
            }

            var doc = _GetXmlDocument(compoundId);
            if(doc == null)
            {
                return "";
            }

            var locationEle = doc.Select("./compounddef/location");
            if(!locationEle.MoveNext())
            {
                return "";
            }
            return locationEle.Current.GetAttribute("file", "");
        }

        Dictionary<string, List<int>> _GetCodeRefs(string fileId, int startLine, int endLine)
        {
            Dictionary<string, List<int>> refDict = new Dictionary<string, List<int>>();
            if (fileId == "" || endLine < startLine || startLine < 0)
            {
                return refDict;
            }

            var doc = _GetXmlDocument(fileId);
            if (doc == null)
            {
                return refDict;
            }

            var programList = doc.Select("./compounddef/programlisting");
            while (programList.MoveNext())
            {
                var lineEle = programList.Current;
                var lineNumberAttr = lineEle.GetAttribute("lineno","");
                int lineNumber = Convert.ToInt32(lineNumberAttr);
                if (lineNumber < startLine || lineNumber > endLine)
                {
                    continue;
                }

                var refIter = lineEle.Select("./highlight/ref");
                while (refIter.MoveNext())
                {
                    var refObj = refIter.Current;
                    var refId = refObj.GetAttribute("refid", "");
                    if (refDict.ContainsKey(refId))
                    {
                        refDict[refId].Add(lineNumber);
                    }
                    else
                    {
                        refDict[refId] = new List<int> { lineNumber };
                    }
                }
            }
            return refDict;
        }

        void _ReadIndex()
        {
            if (m_dbFolder == "")
            {
                return;
            }

            var doc = _GetXmlDocument("index");

            var compoundIter = doc.Select("compound");
            while (compoundIter.MoveNext())
            {
                var compound = compoundIter.Current;
                var compoundRefId = compound.GetAttribute("refid","");

                // Record name attr
                var compoundChildIter = compound.SelectChildren(XPathNodeType.Element);
                while (compoundChildIter.MoveNext())
                {
                    var compoundChild = compoundChildIter.Current;
                    if (compoundChild.Name == "name")
                    {
                        m_idInfoDict[compoundRefId] = new IndexItem(compoundChild.Value, compoundChild.GetAttribute("kind", ""), compoundRefId);
                    }

                    // list members
                    var memberIter = compound.Select("member");
                    List<string> refIdList = new List<string>();
                    while (memberIter.MoveNext())
                    {
                        var member = memberIter.Current;
                        // build member -> compound dict
                        var memberRefId = member.GetAttribute("refid","");
                        m_idToCompoundDict[memberRefId] = compoundRefId;
                        refIdList.Add(memberRefId);

                        // record name attr
                        var memberChildIter = member.SelectChildren(XPathNodeType.Element);
                        while (memberChildIter.MoveNext())
                        {
                            var memberChild = memberChildIter.Current;
                            if (memberChild.Name == "name")
                            {
                                m_idInfoDict[memberRefId] = new IndexItem(memberChild.Value, member.GetAttribute("kind", ""), memberRefId);
                                break;
                            }
                        }
                    }

                    m_compoundToIdDict[compoundRefId] = refIdList;
                }
            }
        }

        void _ParseRefLocation(XPathNavigator refElement, out string filePath, out int startLine)
        {
            var fileCompoundId = refElement.GetAttribute("compoundref", "");
            filePath = _GetCompoundPath(fileCompoundId);
            startLine = Convert.ToInt32(refElement.GetAttribute("startline", ""));
        }

        void _ReadMemberRef(XPathNavigator memberDef)
        {
            if (memberDef.Name != "memberdef")
            {
                return;
            }

            var memberId = memberDef.GetAttribute("id", "");
            if (!m_idInfoDict.ContainsKey(memberId))
            {
                return;
            }
            var memberItem = m_idInfoDict[memberId];

            // ref location dict for functions
            var memberRefDict = new Dictionary<string, List<int>>();

            var memberChildIter = memberDef.SelectChildren(XPathNodeType.Element);
            while (memberChildIter.MoveNext())
            {
                var memberChild = memberChildIter.Current;
                if (memberChild.Name == "references")
                {
                    var referenceId = memberChild.GetAttribute("refid","");

                    // build the reference dict first
                    if (memberRefDict.Count == 0)
                    {
                        var refElement = _GetXmlElement(referenceId);
                        XPathNodeIterator refElementIter = null;
                        if (refElement != null)
                        {
                            refElementIter = refElement.Select(string.Format("./referencedby[@refid=\'{0}\']", memberId));
                            refElement = refElementIter.Current;
                        }
                        if (refElementIter != null && refElementIter.MoveNext())
                        {
                            var fileCompoundId = refElement.GetAttribute("compoundref","");
                            var memberFilePath = _GetCompoundPath(fileCompoundId);
                            var startLine = Convert.ToInt32(refElement.GetAttribute("startline", ""));
                            var endLine = Convert.ToInt32(refElement.GetAttribute("endline", ""));
                            memberRefDict = _GetCodeRefs(fileCompoundId, startLine, endLine);
                        }
                    }

                    // TODO: more code....
                }
            }
        }

        XPathNavigator _GetXmlElement(string refid)
        {
            if (m_dbFolder == "")
            {
                return null;
            }

            if (m_xmlElementCache.ContainsKey(refid))
            {
                return m_xmlElementCache[refid];
            }

            if (m_idToCompoundDict.ContainsKey(refid))
            {
                var fileName = m_idToCompoundDict[refid];
                var doc = _GetXmlDocument(fileName);
                var memberIter = doc.Select(string.Format("./compounddef/sectiondef/memberdef[@id=\'{0}\']", refid));
                while (memberIter.MoveNext())
                {
                    var member = memberIter.Current;
                    m_xmlElementCache[refid] = member;
                    return member;
                }
            }
            else if (m_compoundToIdDict.ContainsKey(refid))
            {
                var doc = _GetXmlDocument(refid);
                var compoundIter = doc.Select(string.Format("compounddef[@id=\'{0}\']", refid));
                while (compoundIter.MoveNext())
                {
                    var compound = compoundIter.Current;
                    m_xmlElementCache[refid] = compound;
                    return compound;
                }
            }
            return null;
        }
    }
}
