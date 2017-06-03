using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace DoxygenDB
{
    public class Variant
    {
        public Variant(string str) { m_string = str; }
        public Variant(int val) { m_int = val; }
        public string m_string;
        public int m_int;
    }

    public enum EntKind
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

    public enum RefKind
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

    class IndexItem
    {
        public static Dictionary<string, EntKind> s_kindDict = new Dictionary<string, EntKind>{
            {"unknown", EntKind.UNKNOWN},      {"class", EntKind.CLASS},          {"struct", EntKind.STRUCT},
            {"union", EntKind.UNION},          {"interface", EntKind.INTERFACE},  {"protocol", EntKind.PROTOCOL},
            {"category", EntKind.CATEGORY},    {"exception", EntKind.EXCEPTION},  {"file", EntKind.FILE},
            {"namespace", EntKind.NAMESPACE},  {"group", EntKind.GROUP},          {"page", EntKind.PAGE},
            {"example", EntKind.EXAMPLE},      {"dir", EntKind.DIR},              {"define", EntKind.DEFINE},
            {"property", EntKind.PROPERTY},    {"event", EntKind.EVENT},          {"variable", EntKind.VARIABLE},
            {"typedef", EntKind.TYPEDEF},      {"enum", EntKind.ENUM},            {"enumvalue", EntKind.ENUMVALUE},
            {"function", EntKind.FUNCTION},    {"signal", EntKind.SIGNAL},        {"prototype", EntKind.PROTOTYPE},
            {"friend", EntKind.FRIEND},        {"dcop", EntKind.DCOP},            {"slot", EntKind.SLOT},
            // extra keywords
            {"method", EntKind.FUNCTION},      {"object", EntKind.VARIABLE},
        };

        public string m_id;
        public string m_name;
        public EntKind m_kind;
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
        public static Dictionary<string, Tuple<RefKind, bool>> s_kindDict = new Dictionary<string, Tuple<RefKind, bool>>
        {
            {"reference"       , new Tuple<RefKind, bool>(RefKind.UNKNOWN,  false)},
            {"unknown"         , new Tuple<RefKind, bool>(RefKind.UNKNOWN,  false)},
            {"call"            , new Tuple<RefKind, bool>(RefKind.CALL,     false)},
            {"callby"          , new Tuple<RefKind, bool>(RefKind.CALL,     true)},
            {"base"            , new Tuple<RefKind, bool>(RefKind.DERIVE,   true)},
            {"derive"          , new Tuple<RefKind, bool>(RefKind.DERIVE,   false)},
            {"use"             , new Tuple<RefKind, bool>(RefKind.USE,      false)},
            {"useby"           , new Tuple<RefKind, bool>(RefKind.USE,      true)},
            {"member"          , new Tuple<RefKind, bool>(RefKind.MEMBER,   false)},
            {"memberin"        , new Tuple<RefKind, bool>(RefKind.MEMBER,   true)},
            {"declare"         , new Tuple<RefKind, bool>(RefKind.DECLARE,  false)},
            {"define"          , new Tuple<RefKind, bool>(RefKind.DEFINE,   false)},
            {"declarein"       , new Tuple<RefKind, bool>(RefKind.DECLARE,  true)},
            {"definein"        , new Tuple<RefKind, bool>(RefKind.DEFINE,   true)},
            {"override"        , new Tuple<RefKind, bool>(RefKind.OVERRIDE, true)},
            {"overrides"       , new Tuple<RefKind, bool>(RefKind.OVERRIDE, true)},
            {"overriddenby"    , new Tuple<RefKind, bool>(RefKind.OVERRIDE, false)},
        };

        public string m_srcId;
        public string m_dstId;
        public RefKind m_kind;
        public string m_file;
        public int m_line;
        public int m_column;

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

    class XmlDocItem
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

    public class Entity
    {
        public string m_id;
        public string m_shortName;
        public string m_longName;
        public string m_kindName;
        public Dictionary<string, Variant> m_metric = new Dictionary<string, Variant>();

        public Entity(string id, string name, string longName, string kindName, Dictionary<string, Variant> metric)
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

        public Dictionary<string, Variant> Metric()
        {
            return m_metric;
        }
    }

    public class Reference
    {
        public RefKind m_kind;
        public string m_entityId;
        public Entity m_entity;
        public string m_fileName;
        public int m_lineNum;
        public int m_columnNum;

        public Reference(RefKind kind, Entity entity, string file, int line, int column)
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
            return new Entity("", m_fileName, m_fileName, "file", new Dictionary<string, Variant>());
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

    public class DoxygenDB
    {
        string m_dbFolder = "";
        string m_doxyFileFolder = "";
        Dictionary<string, string> m_idToCompoundDict = new Dictionary<string, string>();
        Dictionary<string, List<string>> m_compoundToIdDict = new Dictionary<string, List<string>>();
        Dictionary<string, IndexItem> m_idInfoDict = new Dictionary<string, IndexItem>();
        Dictionary<string, XmlDocItem> m_xmlCache = new Dictionary<string, XmlDocItem>();
        Dictionary<string, XPathNavigator> m_xmlElementCache = new Dictionary<string, XPathNavigator>();
        Dictionary<string, List<string>> m_metaDict = new Dictionary<string, List<string>>();

        public DoxygenDB() { }

        void _ReadDoxyfile(string filePath)
        {
            StreamReader sr = new StreamReader(filePath, Encoding.Default);
            string line;
            string currentKey = "";
            List<string> currentValue = new List<string>();
            while ((line = sr.ReadLine()) != null)
            {
                //Console.WriteLine(line.ToString());
                line = line.Trim();
                if (line == "")
                {
                    continue;
                }

                if (line.StartsWith("#"))
                {
                    continue;
                }

                var lineList = line.Split('=');
                if (lineList.Length == 2)
                {
                    if (currentKey != "")
                    {
                        m_metaDict[currentKey] = currentValue;
                    }
                    currentKey = lineList[0].Trim();
                    var value = lineList[1].Trim('\\');
                    value = value.Trim();
                    currentValue = new List<string> { value };
                }
                else if (lineList.Length == 1)
                {
                    // a value
                    var value = lineList[0].Trim('\\');
                    value = value.Trim();
                    currentValue.Add(value);
                }
            }
        }

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
            // remove bad characters
            var rawContent = File.ReadAllText(filePath, Encoding.UTF8);
            var content = new string(rawContent.Where(c => !char.IsControl(c)).ToArray());
            var bytes = Encoding.UTF8.GetBytes(content);
            var xmlDoc = new XPathDocument(new MemoryStream(bytes));

            //var xmlSetting = new XmlReaderSettings();
            //xmlSetting.CheckCharacters = false;
            //var xmlDoc = new XPathDocument(XmlReader.Create(filePath, xmlSetting));
            //var xmlDoc = new XPathDocument(filePath);
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

            var locationEle = doc.Select("doxygen/compounddef/location");
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

            var programList = doc.Select("doxygen/compounddef/programlisting/codeline");
            while (programList.MoveNext())
            {
                var lineEle = programList.Current;
                var lineNumberAttr = lineEle.GetAttribute("lineno","");
                int lineNumber = Convert.ToInt32(lineNumberAttr);
                if (lineNumber < startLine || lineNumber > endLine)
                {
                    continue;
                }

                var refIter = lineEle.Select("highlight/ref");
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

            var compoundIter = doc.Select("doxygenindex/compound");
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
                        m_idInfoDict[compoundRefId] = new IndexItem(compoundChild.Value, compound.GetAttribute("kind", ""), compoundRefId);
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

        bool _ParseRefLocation(XPathNavigator refElement, out string filePath, out int startLine)
        {
            var fileCompoundId = refElement.GetAttribute("compoundref", "");
            if (fileCompoundId == "")
            {
                filePath = "";
                startLine = 0;
                return false;
            }
            filePath = _GetCompoundPath(fileCompoundId);
            startLine = Convert.ToInt32(refElement.GetAttribute("startline", ""));
            return true;
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
            string memberFilePath = "";

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
                            memberFilePath = _GetCompoundPath(fileCompoundId);
                            var startLine = Convert.ToInt32(refElement.GetAttribute("startline", ""));
                            var endLine = Convert.ToInt32(refElement.GetAttribute("endline", ""));
                            memberRefDict = _GetCodeRefs(fileCompoundId, startLine, endLine);
                        }
                    }

                    if (m_idInfoDict.ContainsKey(referenceId))
                    {
                        var referenceItem = m_idInfoDict[referenceId];
                        string filePath;
                        int startLine;
                        _ParseRefLocation(memberChild, out filePath, out startLine);
                        if (memberRefDict.ContainsKey(referenceId))
                        {
                            startLine = memberRefDict[referenceId][0];
                            filePath = memberFilePath;
                        }
                        var refItem = new IndexRefItem(memberId, referenceId, "unknown", filePath, startLine);
                        memberItem.AddRefItem(refItem);
                        referenceItem.AddRefItem(refItem);
                    }
                }

                if (memberChild.Name == "referenceby")
                {
                    var referenceId = memberChild.GetAttribute("refid", "");
                    if (m_idInfoDict.ContainsKey(referenceId))
                    {
                        var referenceItem = m_idInfoDict[referenceId];
                        string filePath;
                        int startLine;
                        _ParseRefLocation(memberChild, out filePath, out startLine);

                        // find the actual position in caller's function body
                        if (referenceItem.m_kind == EntKind.FUNCTION ||
                            referenceItem.m_kind == EntKind.SLOT)
                        {
                            var fileCompoundId = memberChild.GetAttribute("compoundref", "");
                            var endLine = Convert.ToInt32(memberChild.GetAttribute("endline", ""));
                            var memberRefByDict = _GetCodeRefs(fileCompoundId, startLine, endLine);
                            if (memberRefByDict.ContainsKey(memberId))
                            {
                                startLine = memberRefByDict[memberId][0];
                            }
                        }
                        var refItem = new IndexRefItem(referenceId, memberId, "unknown", filePath, startLine);
                        memberItem.AddRefItem(refItem);
                        referenceItem.AddRefItem(refItem);
                    }
                }

                // find override methods
                if (memberChild.Name == "reimplementedby")
                {
                    var overrideId = memberChild.GetAttribute("refid", "");
                    if (m_idInfoDict.ContainsKey(overrideId))
                    {
                        var overrideItem = m_idInfoDict[overrideId];
                        string filePath;
                        int startLine;
                        _ParseRefLocation(memberChild, out filePath, out startLine);
                        var refItem = new IndexRefItem(memberId, overrideId, "overrides", filePath, startLine);
                        overrideItem.AddRefItem(refItem);
                        memberItem.AddRefItem(refItem);
                    }
                }
                
                if (memberChild.Name == "reimplements")
                {
                    var interfaceId = memberChild.GetAttribute("refid", "");
                    if (m_idInfoDict.ContainsKey(interfaceId))
                    {
                        var interfaceItem = m_idInfoDict[interfaceId];
                        string filePath;
                        int startLine;
                        _ParseRefLocation(memberChild, out filePath, out startLine);
                        var refItem = new IndexRefItem(memberId, memberId, "overrides", filePath, startLine);
                        interfaceItem.AddRefItem(refItem);
                        memberItem.AddRefItem(refItem);
                    }
                }
            }
        }

        void _ReadRef(string compoundFileId)
        {
            var doc = _GetXmlDocument(compoundFileId);
            if (doc == null)
            {
                return;
            }

            var xmlDocItem = _GetXmlDocumentItem(compoundFileId);
            if (xmlDocItem.GetCacheStatus(XmlDocItem.CacheStatus.REF))
            {
                return;
            }

            // build references
            string filePath = "";
            int startLine = 0;
            var compoundDefIter = doc.Select("doxygen/compounddef");
            while (compoundDefIter.MoveNext())
            {
                var compoundDef = compoundDefIter.Current;
                var compoundId = compoundDef.GetAttribute("id", "");

                if (!m_idInfoDict.ContainsKey(compoundId))
                {
                    continue;
                }
                var compoundItem = m_idInfoDict[compoundId];

                var compoundChildrenIter = compoundDef.SelectChildren(XPathNodeType.Element);
                while (compoundChildrenIter.MoveNext())
                {
                    var compoundChild = compoundChildrenIter.Current;
                    // find base classes
                    if (compoundChild.Name == "basecompoundref")
                    {
                        var baseCompoundId = compoundChild.GetAttribute("refid","");
                        if (m_idInfoDict.ContainsKey(baseCompoundId))
                        {
                            var baseCompoundItem = m_idInfoDict[baseCompoundId];
                            _ParseRefLocation(compoundChild, out filePath, out startLine);
                            var refItem = new IndexRefItem(baseCompoundId, compoundId, "derive", filePath, startLine);
                            baseCompoundItem.AddRefItem(refItem);
                            compoundItem.AddRefItem(refItem);
                        }
                    }

                    // find derived classes
                    if (compoundChild.Name == "derivedcompoundref")
                    {
                        var derivedCompoundId = compoundChild.GetAttribute("refid", "");
                        if (m_idInfoDict.ContainsKey(derivedCompoundId))
                        {
                            var derivedCompoundItem = m_idInfoDict[derivedCompoundId];
                            _ParseRefLocation(compoundChild, out filePath, out startLine);
                            var refItem = new IndexRefItem(compoundId, derivedCompoundId, "derive", filePath, startLine);
                            derivedCompoundItem.AddRefItem(refItem);
                            compoundItem.AddRefItem(refItem);
                        }
                    }

                    // find member's refs
                    if (compoundChild.Name == "sectiondef")
                    {
                        var sectionIter = compoundChild.SelectChildren(XPathNodeType.Element);
                        while (sectionIter.MoveNext())
                        {
                            var sectionChild = sectionIter.Current;
                            if (sectionChild.Name == "memberdef")
                            {
                                _ReadMemberRef(sectionChild);

                                var member = sectionChild;
                                var memberId = member.GetAttribute("id", "");
                                if (m_idInfoDict.ContainsKey(memberId))
                                {
                                    var memberItem = m_idInfoDict[memberId];
                                    if (compoundItem != null)
                                    {
                                        var memberLocationIter = member.Select("./location");
                                        if (memberLocationIter.MoveNext())
                                        {
                                            var locationDict = _ParseLocationDict(memberLocationIter.Current);
                                            filePath = locationDict["file"].m_string;
                                            startLine = locationDict["line"].m_int;
                                            var refItem = new IndexRefItem(compoundId, memberId, "member", filePath, startLine);
                                            memberItem.AddRefItem(refItem);
                                            compoundItem.AddRefItem(refItem);
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("not find");
                                }
                            }
                        }
                    }
                }
                // TODO: more code
            }

            xmlDocItem.SetCacheStatus(XmlDocItem.CacheStatus.REF);
        }

        void _ReadRefs()
        {
            if (m_dbFolder == "")
            {
                return;
            }

            foreach(var compoundItem in m_compoundToIdDict)
            {
                Console.WriteLine("Parsing:" + compoundItem.Key);
                _ReadRef(compoundItem.Key);
            }
        }

        bool _IsCompound(string refid)
        {
            return m_compoundToIdDict.ContainsKey(refid);
        }

        bool _IsMember(string refid)
        {
            return m_idToCompoundDict.ContainsKey(refid);
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
                var memberIter = doc.Select(string.Format("doxygen/compounddef/sectiondef/memberdef[@id=\'{0}\']", refid));
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
                var compoundIter = doc.Select(string.Format("doxygen/compounddef[@id=\'{0}\']", refid));
                while (compoundIter.MoveNext())
                {
                    var compound = compoundIter.Current;
                    m_xmlElementCache[refid] = compound;
                    return compound;
                }
            }
            return null;
        }

        Dictionary<string, Variant> _ParseLocationDict(XPathNavigator element)
        {
            var declLineAttr = element.GetAttribute("line", "");
            var declColumnAttr = element.GetAttribute("column", "");

            var declLine = Convert.ToInt32(declLineAttr == "" ? "0" : declLineAttr);
            var declColumn = Convert.ToInt32(declColumnAttr == "" ? "0" : declColumnAttr);
            var declFile = element.GetAttribute("file", "");
            if (!declFile.Contains(":/"))
            {
                declFile = m_doxyFileFolder + "/" + declFile;
            }

            var bodyStartAttr = element.GetAttribute("bodystart", "");
            if (bodyStartAttr == "")
            {
                bodyStartAttr = "0";
            }
            var bodyEndAttr = element.GetAttribute("bodyend", "");
            if (bodyEndAttr == "")
            {
                bodyEndAttr = bodyStartAttr;
            }

            var bodyStart = Convert.ToInt32(bodyStartAttr);
            var bodyEnd = Convert.ToInt32(bodyEndAttr);
            var bodyFile = element.GetAttribute("bodyfile", "");
            if (!bodyFile.Contains(":/"))
            {
                bodyFile = m_doxyFileFolder + "/" + bodyFile;
            }

            if (bodyEnd < 0)
            {
                bodyEnd = bodyStart;
            }

            return new Dictionary<string, Variant> {
                { "file", new Variant(bodyFile) },
                { "line", new Variant(bodyStart) },
                { "column", new Variant(bodyEnd) },
                { "CountLine", new Variant(Math.Max(bodyEnd - bodyStart, 0)) },
                { "declLine", new Variant(declLine) },
                { "declColumn", new Variant(declColumn) },
                { "declFile", new Variant(declFile) },
            };
        }

        Entity _ParseEntity(XPathNavigator element)
        {
            if (element == null)
            {
                return null;
            }

            if (element.Name == "compounddef")
            {
                var name = "";
                var longName = "";
                var kind = element.GetAttribute("kind", "");
                Dictionary<string, Variant> metric = null;
                var id = element.GetAttribute("id", "");
                var childrenIter = element.SelectChildren(XPathNodeType.Element);
                while (childrenIter.MoveNext())
                {
                    var elementChild = childrenIter.Current;
                    if (elementChild.Name == "compoundname")
                    {
                        name = elementChild.Value;
                        longName = name;
                    }
                    else if (elementChild.Name == "location")
                    {
                        metric = _ParseLocationDict(elementChild);
                    }
                }
                return new Entity(id, name, longName, kind, metric);
            }
            else if (element.Name == "memberdef")
            {
                var name = "";
                var longName = "";
                var kind = element.GetAttribute("kind","");
                var virt = element.GetAttribute("virt","");
                if (virt == "virtual")
                {
                    kind = "virtual " + kind; 
                }
                else if (virt == "pure-virtual")
                {
                    kind = "pure virtual " + kind;
                }
                Dictionary<string, Variant> metric = null;
                var id = element.GetAttribute("id", "");
                var elementChildIter = element.SelectChildren(XPathNodeType.Element);
                while (elementChildIter.MoveNext())
                {
                    var elementChild = elementChildIter.Current;
                    if (elementChild.Name == "name")
                    {
                        name = elementChild.Value;
                    }
                    else if (elementChild.Name == "definition")
                    {
                        longName = elementChild.Value;
                    }
                    else if (elementChild.Name == "location")
                    {
                        metric = _ParseLocationDict(elementChild);
                    }
                }
                return new Entity(id, name, longName, kind, metric);
            }
            else
            {
                return null;
            }
        }

        public void Open(string fullPath)
        {
            if (m_dbFolder != "")
            {
                Close();
            }

            m_doxyFileFolder = System.IO.Path.GetDirectoryName(fullPath);
            m_doxyFileFolder = m_doxyFileFolder.Replace('\\', '/');

            _ReadDoxyfile(fullPath);
            m_dbFolder = m_metaDict["OUTPUT_DIRECTORY"][0];
            m_dbFolder += "/" + m_metaDict["XML_OUTPUT"][0];
            m_dbFolder = m_dbFolder.Replace('\\', '/');

            _ReadIndex();
            //_ReadRefs();
        }

        public string GetDBPath()
        {
            return m_dbFolder + "/index.xml";
        }

        public void Close()
        {
            m_doxyFileFolder = "";
            m_dbFolder = "";
            m_idToCompoundDict.Clear();
            m_compoundToIdDict.Clear();
            m_idInfoDict.Clear();
            m_xmlCache.Clear();
            m_xmlElementCache.Clear();
        }

        public void Reopen()
        {
            // TODO: Add code
        }

        public void Analyze()
        {
            // TODO: Add code
        }

        public void OnOpen()
        {
            // TODO: Add code
        }

        public List<Entity> Search(string name, string kindString = "")
        {
            var res = new List<Entity>();
            if (name == null || name == "")
            {
                return res;
            }

            var kindStr = kindString.ToLower();
            var kind = EntKind.UNKNOWN;
            if (IndexItem.s_kindDict.ContainsKey(kindStr))
            {
                kind = IndexItem.s_kindDict[kindStr];
            }
            var nameLower = name.ToLower();
            foreach (var item in m_idInfoDict)
            {
                if (kind != EntKind.UNKNOWN && item.Value.m_kind != kind)
                {
                    continue;
                }
                if (item.Value.m_name.ToLower().Contains(nameLower))
                {
                    var xmlElement = _GetXmlElement(item.Key);
                    if (xmlElement == null)
                    {
                        continue;
                    }
                    var entity = _ParseEntity(xmlElement);
                    res.Add(entity);
                }
            }
            return res;
        }

        public Entity SearchFromUniqueName(string uniqueName)
        {
            if (m_dbFolder == null || m_dbFolder == "")
            {
                return null;
            }

            var xmlElement = _GetXmlElement(uniqueName);
            if (xmlElement == null)
            {
                return null;
            }
            var entity = _ParseEntity(xmlElement);
            return entity;
        }

        public void _SearchRef(out List<Entity> refEntityList, out List<Reference> refRefList, string uniqueName, string refKindStr = "", string entKindStr = "", bool isUnique = true)
        {
            refEntityList = new List<Entity>();
            refRefList = new List<Reference>();
            if (!m_idInfoDict.ContainsKey(uniqueName))
            {
                return;
            }
            var thisItem = m_idInfoDict[uniqueName];

            // parse refKindStr
            var refKindList = new List<Tuple<RefKind, bool>>();
            if (refKindStr != "")
            {
                refKindStr = refKindStr.ToLower();
                string pattern = @"[a-z]+";
                var matches = Regex.Matches(
                    refKindStr,
                    pattern,
                    RegexOptions.ExplicitCapture
                    );

                foreach (Match nextMatch in matches)
                {
                    var kind = IndexRefItem.s_kindDict[nextMatch.Value];
                    refKindList.Add(kind);
                }
            }
            else
            {
                refKindList = new List<Tuple<RefKind, bool>>( IndexRefItem.s_kindDict.Values);
            }

            // parse entKindStr
            var entKindList = new List<EntKind>();
            if (entKindStr != "")
            {
                var entKindNameStr = entKindStr.ToLower();
                string pattern = @"[a-z]+";
                var matches = Regex.Matches(
                    entKindNameStr,
                    pattern,
                    RegexOptions.ExplicitCapture
                    );

                foreach (Match nextMatch in matches)
                {
                    var kind = IndexItem.s_kindDict[nextMatch.Value];
                    entKindList.Add(kind);
                }
            }

            // build reference link
            var compoundId = uniqueName;
            if (m_idToCompoundDict.ContainsKey(uniqueName))
            {
                compoundId = m_idToCompoundDict[uniqueName];
            }
            _ReadRef(compoundId);

            // find references
            var refs = thisItem.GetRefItemList();
            var thisEntity = SearchFromUniqueName(uniqueName);
            foreach (var refObj in refs)
            {
                var otherId = refObj.m_srcId;
                if (refObj.m_srcId == uniqueName)
                {
                    otherId = refObj.m_dstId;
                }

                IndexItem otherItem;
                if (!m_idInfoDict.TryGetValue(otherId, out otherItem))
                {
                    continue;
                }
                if (entKindList.Count > 0 && !entKindList.Contains(otherItem.m_kind))
                {
                    continue;
                }
                var otherEntity = SearchFromUniqueName(otherId);
                if (otherEntity == null)
                {
                    continue;
                }

                // match each ref kind
                foreach (var item in refKindList)
                {
                    var refKind = item.Item1;
                    var isExchange = item.Item2;
                    var srcItem = thisItem;
                    var dstItem = otherItem;
                    var dstMetric = otherEntity.m_metric;
                    if (isExchange)
                    {
                        srcItem = otherItem;
                        dstItem = thisItem;
                        dstMetric = thisEntity.m_metric;
                    }

                    // check edge direction
                    if (srcItem.m_id != refObj.m_srcId || dstItem.m_id != refObj.m_dstId)
                    {
                        continue;
                    }

                    var isAccepted = false;
                    var file = refObj.m_file;
                    var line = refObj.m_line;
                    var column = refObj.m_column;
                    if (refKind == RefKind.CALL && refObj.m_kind == RefKind.UNKNOWN)
                    {
                        if (srcItem.m_kind == EntKind.FUNCTION && dstItem.m_kind == EntKind.FUNCTION)
                        {
                            isAccepted = true;
                        }
                    }
                    else if (refKind == RefKind.DEFINE && refObj.m_kind == RefKind.MEMBER)
                    {
                        isAccepted = true;
                        file = dstMetric["file"].m_string;
                        line = dstMetric["line"].m_int;
                        column = dstMetric["column"].m_int;
                    }
                    else if ((refKind == RefKind.MEMBER || refKind == RefKind.DECLARE) && refObj.m_kind == RefKind.MEMBER)
                    {
                        isAccepted = true;
                        file = dstMetric["declFile"].m_string;
                        line = dstMetric["declLine"].m_int;
                        column = dstMetric["declColumn"].m_int;
                    }
                    else if (refKind == RefKind.USE && refObj.m_kind == RefKind.UNKNOWN)
                    {
                        if (srcItem.m_kind == EntKind.FUNCTION && dstItem.m_kind == EntKind.VARIABLE)
                        {
                            isAccepted = true;
                        }
                    }
                    else if (refKind == RefKind.DERIVE && refObj.m_kind == RefKind.DERIVE)
                    {
                        if ((srcItem.m_kind == EntKind.CLASS || srcItem.m_kind == EntKind.STRUCT) && 
                            (dstItem.m_kind == EntKind.CLASS || dstItem.m_kind == EntKind.STRUCT))
                        {
                            isAccepted = true;
                        }
                    }
                    else if (refKind == RefKind.OVERRIDE && refObj.m_kind == RefKind.OVERRIDE)
                    {
                        isAccepted = true;
                    }

                    if (isAccepted)
                    {
                        refEntityList.Add(otherEntity);
                        refRefList.Add(new Reference(refKind, otherEntity, file, line, column));
                    }
                }
            }
        }

        public void SearchRefEntity(out List<Entity> refEntityList, out List<Reference> refRefList,
            string uniqueName, string refKindStr = "", string entKindStr = "", bool isUnique = true)
        {
            _SearchRef(out refEntityList, out refRefList, uniqueName, refKindStr, entKindStr, isUnique);
        }

        public Reference SearchRefObj(string srcUName, string tarUName)
        {
            List<Entity> refEntityList = new List<Entity>();
            List<Reference> refRefList = new List<Reference>();
            _SearchRef(out refEntityList, out refRefList, srcUName);
            for (int i = 0; i < refEntityList.Count; i++)
            {
                if (refEntityList[i].UniqueName() == tarUName)
                {
                    return refRefList[i];
                }
            }
            return null;
        }

        public List<Reference> SearchRef(
            string uniqueName, string refKindStr = "", string entKindStr = "", bool isUnique = true)
        {
            var refEntityList = new List<Entity>();
            var refRefList = new List<Reference>();
            _SearchRef(out refEntityList, out refRefList, uniqueName, refKindStr, entKindStr, isUnique);
            return refRefList;
        }

        public void SearchCallPaths(string srcUniqueName, string tarUniqueName)
        {
            // TODO: Add code
        }
    }
}
