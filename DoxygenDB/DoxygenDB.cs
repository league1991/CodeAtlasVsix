using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        UNKNOWN  = 0x0,
        MEMBER   = 0x1,
        CALL     = 0x2,
        DERIVE   = 0x4,
        USE      = 0x8,
        OVERRIDE = 0x10,
        DECLARE  = 0x20,
        DEFINE   = 0x40,
        ALL      = 0x3fffffff,
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
            {"type", EntKind.TYPEDEF},         {"enum", EntKind.ENUM},            {"enumvalue", EntKind.ENUMVALUE},
            {"function", EntKind.FUNCTION},    {"signal", EntKind.SIGNAL},        {"prototype", EntKind.PROTOTYPE},
            {"friend", EntKind.FRIEND},        {"dcop", EntKind.DCOP},            {"slot", EntKind.SLOT},
            // extra keywords
            {"method", EntKind.FUNCTION},      {"object", EntKind.VARIABLE},      {"typedef", EntKind.TYPEDEF},
            {"service", EntKind.UNKNOWN},      {"singleton", EntKind.UNKNOWN},    {"module", EntKind.UNKNOWN},
        };

        public string m_id;
        public string m_name;
        public EntKind m_kind;
        public HashSet<IndexRefItem> m_refs = new HashSet<IndexRefItem>();
        public int m_cacheStatus;

        public enum CacheStatus
        {
            NONE = 0,
            REF = 1
        }

        public IndexItem(string name, string kindStr, string id)
        {
            m_name = name;
            int idx = m_name.LastIndexOf("::");
            if (idx != -1 && idx < m_name.Length - 1)
            {
                m_name = m_name.Substring(idx + 2);
            }
            m_id = id;
            m_kind = s_kindDict[kindStr];
            m_cacheStatus = (int)CacheStatus.NONE;
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

        public void ClearRefItem()
        {
            m_refs.Clear();
        }

        public bool GetCacheStatus(CacheStatus status)
        {
            return (m_cacheStatus & (int)status) > 0;
        }

        public void SetCacheStatus(CacheStatus status)
        {
            m_cacheStatus |= (int)status;
        }

        public List<IndexRefItem> GetRefItemList()
        {
            return m_refs.ToList();
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
            {"include"         , new Tuple<RefKind, bool>(RefKind.USE,      false)},
            {"includedby"      , new Tuple<RefKind, bool>(RefKind.USE,      true)},
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

        public IndexRefItem(string srcId, string dstId, string refKindStr, string file = "", int line = -1, int column = -1)
        {
            m_srcId = srcId;
            m_dstId = dstId;
            m_kind = s_kindDict[refKindStr].Item1;
            m_file = file;
            m_line = line;
            m_column = column;
            if (file == "")
            {
                m_line = m_column = -1;
            }
        }

        public override bool Equals(object obj)
        {
            IndexRefItem e = obj as IndexRefItem;
            if (e == null)
            {
                return false;
            }
            return e.m_srcId == m_srcId && e.m_dstId == m_dstId &&
                e.m_kind == m_kind && e.m_file == m_file &&
                e.m_line == m_line && e.m_column == m_column;
        }

        public override int GetHashCode()
        {
            return m_srcId.GetHashCode() ^ m_dstId.GetHashCode() ^ m_kind.GetHashCode() ^ m_file.GetHashCode() ^ m_line ^ m_column;
        }
    }

    class XmlDocItem
    {
        static int m_maxXmlCount = 1000;
        static long s_maxXmlSize = 350 * 1024 * 1024; // 400 MB
        static long s_currentXmlSize = 0;
        // filePath -> xml doc
        static LinkedList<XmlDocItem> s_xmlLRUList = new LinkedList<XmlDocItem>();
        static Dictionary<string, LinkedListNode<XmlDocItem>> s_lruDict = new Dictionary<string, LinkedListNode<XmlDocItem>>();
        static Dictionary<string, XPathNavigator> s_xmlElementCache = new Dictionary<string, XPathNavigator>();
        static string s_emptyXml = "<?xml version='1.0' encoding='UTF-8' standalone='no'?><doxygen></doxygen>";

        public enum CacheStatus
        {
            NONE = 0,
            REF = 1
        }

        XPathDocument m_doc;
        XPathNavigator m_navigator;
        HashSet<string> m_elementCache = new HashSet<string>();
        public int m_cacheStatus;
        string m_filePath;

        public XmlDocItem(string filePath)
        {
            m_filePath = filePath;
            _LoadXml();
            m_cacheStatus = (int)CacheStatus.NONE;
        }

        void _LoadXml()
        {
            while (s_xmlLRUList.Count >= m_maxXmlCount || s_currentXmlSize > s_maxXmlSize)
            {
                var lastXmlDoc = s_xmlLRUList.Last.Value;
                var lastFileInfo = new FileInfo(lastXmlDoc.m_filePath);
                s_currentXmlSize -= lastFileInfo.Length;
                // remove element reference
                foreach (var docElement in lastXmlDoc.m_elementCache)
                {
                    s_xmlElementCache.Remove(docElement);
                }
                lastXmlDoc.ClearDocument();

                // remove lru
                s_lruDict.Remove(s_xmlLRUList.Last.Value.m_filePath);
                s_xmlLRUList.RemoveLast();
            }

            try
            {
                var t0 = DateTime.Now;
                var t1 = t0;
                var reader = new StreamReader(m_filePath, Encoding.UTF8);
                var doc = new XPathDocument(reader);
                t1 = DateTime.Now;
                Console.WriteLine("------------ Parse " + (t1 - t0).TotalMilliseconds.ToString());
                t0 = t1;
                m_doc = doc;
            }
            catch (Exception)
            {
                // Try to filter out problematic characters
                try
                {
                    var rawContent = File.ReadAllText(m_filePath, Encoding.UTF8);
                    var content = new string(rawContent.Where(c => !char.IsControl(c)).ToArray());
                    var bytes = Encoding.UTF8.GetBytes(content);
                    m_doc = new XPathDocument(new MemoryStream(bytes));
                }
                catch (Exception)
                {
                    // Still invalid, create an empty xml document
                    m_doc = new XPathDocument(new MemoryStream(Encoding.UTF8.GetBytes(s_emptyXml)));
                }
            }

            m_navigator = m_doc.CreateNavigator();

            // update lru
            if (s_lruDict.ContainsKey(m_filePath))
            {
                s_xmlLRUList.Remove(s_lruDict[m_filePath]);
                s_lruDict.Remove(m_filePath);
            }
            var node = s_xmlLRUList.AddFirst(this);
            s_lruDict[m_filePath] = node;

            var fileInfo = new FileInfo(m_filePath);
            s_currentXmlSize += fileInfo.Length;
        }

        public void ClearDocument()
        {
            m_doc = null;
            m_navigator = null;
            m_elementCache.Clear();
        }

        public XPathNavigator GetNavigator()
        {
            if (m_doc == null || m_navigator == null)
            {
                _LoadXml();
            }
            return m_navigator;
        }

        public XPathDocument GetDocument()
        {
            if (m_doc == null || m_navigator == null)
            {
                _LoadXml();
            }
            return m_doc;
        }

        public void AddElementRef(string refId, XPathNavigator nav)
        {
            s_xmlElementCache[refId] = nav;
            m_elementCache.Add(refId);
        }

        public static XPathNavigator GetElementRef(string refID)
        {
            if (s_xmlElementCache.ContainsKey(refID))
            {
                return s_xmlElementCache[refID];
            }
            return null;
        }

        public static void ClearElementCache()
        {
            s_xmlElementCache.Clear();
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
            int idx = m_shortName.LastIndexOf("::");
            if (idx != -1 && idx < m_shortName.Length - 1)
            {
                m_shortName = m_shortName.Substring(idx + 2);
            }
            idx = m_shortName.LastIndexOf("/");
            if (idx != -1 && idx < m_shortName.Length - 1)
            {
                m_shortName = m_shortName.Substring(idx + 1);
            }
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

    public class DoxygenDBConfig
    {
        public string m_configPath = "";
        public string m_projectName = "";
        public List<string> m_inputFolders = new List<string>();
        public string m_outputDirectory = "";
        public List<string> m_includePaths = new List<string>();
        public List<string> m_defines = new List<string>();
        public bool m_useClang = false;
        public string m_mainLanguage = "";
        public Dictionary<string, string> m_customExt = new Dictionary<string, string>();
    }

    public class CodeBlock
    {
        public string m_file;
        public int m_start, m_end;

        public CodeBlock(string file, int start, int end)
        {
            m_file = file;
            m_start = start;
            m_end = end;
        }

        public override bool Equals(object obj)
        {
            CodeBlock e = obj as CodeBlock;
            if (e == null)
            {
                return false;
            }
            return e.m_end == m_end && e.m_start == m_start && e.m_file == m_file;
        }

        public override int GetHashCode()
        {
            return m_end.GetHashCode() ^ m_start.GetHashCode() ^ m_file.GetHashCode();
        }
    }
    
    class BlockRefData
    {
        public Dictionary<string, List<int>> m_idToRowDict;
        public Dictionary<string, List<int>> m_nameToRowDict;
        public BlockRefData(Dictionary<string, List<int>> idToRow, Dictionary<string, List<int>> nameToRow)
        {
            m_idToRowDict = idToRow;
            m_nameToRowDict = nameToRow;
        }
    }

    public class DoxygenDB
    {
        string m_dbFolder = "";
        string m_doxyFileFolder = "";
        static string s_doxygenExePath = Path.Combine(Path.GetTempPath(), "doxygen.exe");

        Dictionary<string, string> m_idToCompoundDict = new Dictionary<string, string>();
        Dictionary<string, List<string>> m_compoundToIdDict = new Dictionary<string, List<string>>();
        Dictionary<string, IndexItem> m_idInfoDict = new Dictionary<string, IndexItem>();
        Dictionary<string, List<string>> m_nameIDDict = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> m_metaDict = new Dictionary<string, List<string>>();
        Dictionary<CodeBlock, BlockRefData> m_blockRefDict = new Dictionary<CodeBlock, BlockRefData>();
        static Regex s_identifierReg = new Regex(@"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // xml management
        Dictionary<string, XmlDocItem> m_xmlCache = new Dictionary<string, XmlDocItem>();

        bool m_resolveReferencePosition = false;

        public DoxygenDB() { }

        static bool _CheckAndExtractDoxygenExe()
        {
            if (s_doxygenExePath == "")
            {
                return false;
            }
            if (File.Exists(s_doxygenExePath))
            {
                return true;
            }
            var currentAssembly = Assembly.GetExecutingAssembly();
            var arrResources = currentAssembly.GetManifestResourceNames();
            foreach (var resourceName in arrResources)
            {
                if (resourceName.ToLower().EndsWith("doxygen.exe"))
                {
                    using (var resourceToSave = currentAssembly.GetManifestResourceStream(resourceName))
                    {
                        using (var output = File.OpenWrite(s_doxygenExePath))
                            resourceToSave.CopyTo(output);
                        resourceToSave.Close();
                    }
                }
            }

            return true;
        }

        public static bool _GenerateConfigureFile(string configPath)
        {
            if (!_CheckAndExtractDoxygenExe())
            {
                return false;
            }
            System.Diagnostics.Process exep = new System.Diagnostics.Process();
            exep.StartInfo.FileName = s_doxygenExePath;
            exep.StartInfo.Arguments = string.Format("-g \"{0}\"", configPath);
            //exep.StartInfo.CreateNoWindow = true;
            exep.StartInfo.UseShellExecute = false;
            exep.Start();
            exep.WaitForExit();
            return true;
        }

        public static void _ReadDoxyfile(string filePath, Dictionary<string, List<string>> metaDict)
        {
            metaDict.Clear();
            StreamReader sr = new StreamReader(filePath, Encoding.Default);
            string line;
            string currentKey = "";
            List<string> currentValue = new List<string>();
            while (true)
            {
                //Console.WriteLine(line.ToString());
                line = sr.ReadLine();
                if (line == null)
                {
                    metaDict[currentKey] = currentValue;
                    break;
                }
                line = line.Trim();
                if (line == "" || line.StartsWith("#"))
                {
                    continue;
                }

                var lineList = line.Split('=');
                if (lineList.Length == 2)
                {
                    if (currentKey != "")
                    {
                        metaDict[currentKey] = currentValue;
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
            sr.Close();
        }

        public static void _WriteDoxyfile(string path, Dictionary<string, List<string>> metaDict)
        {
            string content = "";
            foreach (var itemPair in metaDict)
            {
                content += itemPair.Key + " = ";
                var value = itemPair.Value;
                for (int i = 0; i < value.Count; i++)
                {
                    if (i != 0)
                    {
                        content += " \\\n";
                        content += "     ";
                    }
                    content += value[i];
                }
                content += "\n";
            }
            File.WriteAllText(path, content);
        }

        public static bool _AnalyseDoxyfile(string configPath)
        {
            if (!_CheckAndExtractDoxygenExe())
            {
                return false;
            }
            System.Diagnostics.Process exep = new System.Diagnostics.Process();
            exep.StartInfo.FileName = s_doxygenExePath;
            exep.StartInfo.Arguments = string.Format(" \"{0}\"", configPath);
            //exep.StartInfo.CreateNoWindow = true;
            exep.StartInfo.UseShellExecute = false;
            exep.Start();
            exep.WaitForExit();
            return true;
        }

        static List<string> toPathList(List<string> list)
        {
            var pathList = new List<string>();
            foreach (var item in list)
            {
                pathList.Add("\"" + item + "\"");
            }
            return pathList;
        }

        static List<string> fromPathList(List<string> list)
        {
            var pathList = new List<string>();
            foreach (var item in list)
            {
                string res = item;
                pathList.Add(item.Trim(new char[] {' ', '\t', '\"', '\r', '\n'}));
            }
            return pathList;
        }

        public static bool GenerateDB(DoxygenDBConfig config)
        {
            string configPath = config.m_configPath;
            string projectName = config.m_projectName;
            List<string> inputFolders = config.m_inputFolders;
            string outputDirectory = config.m_outputDirectory;

            if (!_GenerateConfigureFile(configPath))
                return false;
            var metaDict = new Dictionary<string, List<string>>();
            _ReadDoxyfile(configPath, metaDict);

            metaDict["OUTPUT_DIRECTORY"] = toPathList(new List<string> { outputDirectory });
            metaDict["PROJECT_NAME"] = new List<string> { projectName };
            metaDict["EXTRACT_ALL"] = new List<string> { "YES" };
            metaDict["EXTRACT_PRIVATE"] = new List<string> { "YES" };
            metaDict["EXTRACT_PACKAGE"] = new List<string> { "YES" };
            metaDict["EXTRACT_STATIC"] = new List<string> { "YES" };
            metaDict["EXTRACT_LOCAL_CLASSES"] = new List<string> { "YES" };
            metaDict["EXTRACT_LOCAL_METHODS"] = new List<string> { "YES" };
            metaDict["EXTRACT_ANON_NSPACES"] = new List<string> { "YES" };
            metaDict["INPUT"] = toPathList(inputFolders);
            metaDict["RECURSIVE"] = new List<string> { "NO" };
            metaDict["SOURCE_BROWSER"] = new List<string> { "YES" };
            metaDict["REFERENCED_BY_RELATION"] = new List<string> { "YES" };
            metaDict["REFERENCES_RELATION"] = new List<string> { "YES" };
            metaDict["GENERATE_HTML"] = new List<string> { "NO" };
            metaDict["GENERATE_LATEX"] = new List<string> { "NO" };
            metaDict["GENERATE_XML"] = new List<string> { "YES" };
            metaDict["CLASS_DIAGRAMS"] = new List<string> { "NO" };
            metaDict["CLANG_ASSISTED_PARSING"] = new List<string> { config.m_useClang ? "YES" : "NO"};
            metaDict["INCLUDE_PATH"] = toPathList(config.m_includePaths); 
            //metaDict["CPP_CLI_SUPPORT"] = new List<string> { "NO" };
            metaDict["ALLEXTERNALS"] = new List<string> { "YES" };
            metaDict["PREDEFINED"] = config.m_defines;

            if (config.m_mainLanguage == "cpp")
            {
                metaDict["BUILTIN_STL_SUPPORT"] = new List<string> { "YES" };
            }
            else if(config.m_mainLanguage == "csharp")
            {
                metaDict["OPTIMIZE_OUTPUT_JAVA"] = new List<string> { "YES" };
            }
            // metaDict["INPUT_ENCODING"] = new List<string> { "iso-8859-1" };

            var extensionList = new List<string>();
            List<string> filePattern = new List<string> {
                "*.c", "*.cc", "*.cxx", "*.cpp", "*.c++", "*.java", "*.ii", "*.ixx",
                "*.ipp", "*.i++", "*.inl", "*.idl", "*.ddl", "*.odl", "*.h", "*.hh",
                "*.hxx", "*.hpp", "*.h++", "*.cs", "*.d", "*.php", "*.php4", "*.php5",
                "*.phtml", "*.inc", "*.m", "*.markdown", "*.md", "*.mm", "*.dox",
                "*.py", "*.pyw", "*.f90", "*.f95", "*.f03", "*.f08", "*.f",
                "*.for", "*.tcl", "*.vhd", "*.vhdl", "*.ucf", "*.qsf"};
            foreach (var item in config.m_customExt)
            {
                extensionList.Add(item.Key + "=" + item.Value);
                filePattern.Add("*." + item.Key);
            }
            metaDict["EXTENSION_MAPPING"] = extensionList;
            metaDict["FILE_PATTERNS"] = filePattern;

            _WriteDoxyfile(configPath, metaDict);
            return _AnalyseDoxyfile(configPath);
        }


        XPathNavigator _GetXmlDocument(string fileName)
        {
            var item = _GetXmlDocumentItem(fileName);
            return item.GetNavigator();
        }

        XmlDocItem _GetXmlDocumentItem(string fileName)
        {
            var filePath = string.Format("{0}/{1}.xml", m_dbFolder, fileName);
            if (m_xmlCache.ContainsKey(filePath))
            {
                return m_xmlCache[filePath];
            }

            // remove bad characters
            //var rawContent = File.ReadAllText(filePath, Encoding.UTF8);
            //var content = new string(rawContent.Where(c => !char.IsControl(c)).ToArray());
            //var bytes = Encoding.UTF8.GetBytes(content);
            //var xmlDoc = new XPathDocument(new MemoryStream(bytes));

            //var xmlSetting = new XmlReaderSettings();
            //xmlSetting.CheckCharacters = false;
            //var xmlDoc = new XPathDocument(XmlReader.Create(filePath, xmlSetting));
            //var xmlDoc = new XPathDocument(filePath);

            var xmlDocItem = new XmlDocItem(filePath);
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
            if (doc == null)
            {
                return "";
            }

            var locationEle = doc.Select("doxygen/compounddef/location");
            if (!locationEle.MoveNext())
            {
                return "";
            }
            string compoundPath = locationEle.Current.GetAttribute("file", "");
            if (!compoundPath.Contains(":"))
            {
                compoundPath = m_doxyFileFolder + "/" + compoundPath;
            }
            return compoundPath;
        }

        void _GetCodeRefs(string fileId, int startLine, int endLine, 
            out Dictionary<string, List<int>> idToRowDict,
            out Dictionary<string, List<int>> nameToRowDict)
        {
            idToRowDict = new Dictionary<string, List<int>>();
            nameToRowDict = new Dictionary<string, List<int>>();
            
            
            if (fileId == "")
            {
                return;
            }

            var doc = _GetXmlDocument(fileId);
            if (doc == null)
            {
                return;
            }
            
            // Try to find in cache
            CodeBlock block = new CodeBlock(fileId, startLine, endLine);
            BlockRefData refData;
            if (m_blockRefDict.TryGetValue(block, out refData))
            {
                idToRowDict = refData.m_idToRowDict;
                nameToRowDict = refData.m_nameToRowDict;
                return;
            }

            Action<Dictionary<string, List<int>>, string, int> AddToDict = (dict, key, line) =>
            {
                if (dict.ContainsKey(key))
                {
                    dict[key].Add(line);
                }
                else
                {
                    dict[key] = new List<int> { line };
                }
            };

            string codeLineFormat = "doxygen/compounddef/programlisting/codeline";
            
            var programList = doc.Select(codeLineFormat);
            while (programList.MoveNext())
            {
                var lineEle = programList.Current;
                var lineNumberAttr = lineEle.GetAttribute("lineno", "");
                int lineNumber = Convert.ToInt32(lineNumberAttr);
                if (startLine != -1 && lineNumber < startLine)
                {
                    continue;
                }
                if (endLine != -1 && lineNumber > endLine)
                {
                    break;
                }

                var highLightIter = lineEle.Select("highlight");
                while (highLightIter.MoveNext())
                {
                    var highLight = highLightIter.Current;
                    var highLightType = highLight.GetAttribute("class","");
                    if (highLightType == "normal" || highLightType == "preprocessor")
                    {
                        var highLightChildIter = highLight.SelectChildren(XPathNodeType.All);
                        while (highLightChildIter.MoveNext())
                        {
                            var highLightChild = highLightChildIter.Current;
                            if (highLightChild.Name == "ref")
                            {
                                var refObj = highLightChild;
                                var refId = refObj.GetAttribute("refid", "");
                                AddToDict(idToRowDict, refId, lineNumber);
                            }

                            var highLightValue = highLightChild.Value;
                            if (highLightValue != "")
                            {
                                if (highLightChild.Name == "ref" || highLightChild.Name == "")
                                {
                                    //string pattern = @"[a-zA-Z_][a-zA-Z0-9_]*";
                                    //var nameList = Regex.Matches(highLightValue, pattern, RegexOptions.ExplicitCapture);
                                    var nameList = s_identifierReg.Matches(highLightValue);
                                    foreach (Match nextMatch in nameList)
                                    {
                                        var tokenValue = nextMatch.Value;
                                        AddToDict(nameToRowDict, tokenValue, lineNumber);
                                    }
                                }
                            }
                        }
                    }
                }

                //var refIter = lineEle.Select("highlight/ref");
                //while (refIter.MoveNext())
                //{
                //    var refObj = refIter.Current;
                //    var refId = refObj.GetAttribute("refid", "");
                //    if (idToRowDict.ContainsKey(refId))
                //    {
                //        idToRowDict[refId].Add(lineNumber);
                //    }
                //    else
                //    {
                //        idToRowDict[refId] = new List<int> { lineNumber };
                //    }
                //}
            }

            
            m_blockRefDict[block] = new BlockRefData(idToRowDict, nameToRowDict);
            return;
        }

        void _AddToNameIDDict(string name, string id)
        {
            if (!m_nameIDDict.ContainsKey(name))
            {
                m_nameIDDict[name] = new List<string>();
            }
            m_nameIDDict[name].Add(id);
        }

        string _GetCompoundIDFromPath(string filePath)
        {
            int idx = filePath.LastIndexOf("/");
            if (idx != -1 && idx != filePath.Length-1)
            {
                filePath = filePath.Substring(idx+1);
            }
            if (m_nameIDDict.ContainsKey(filePath))
            {
                var list = m_nameIDDict[filePath];
                return list[0];
            }
            return "";
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
                var compoundRefId = compound.GetAttribute("refid", "");

                // Record name attr
                List<string> refIdList = new List<string>();
                var compoundChildIter = compound.SelectChildren(XPathNodeType.Element);
                while (compoundChildIter.MoveNext())
                {
                    var compoundChild = compoundChildIter.Current;
                    if (compoundChild.Name == "name")
                    {
                        string name = compoundChild.Value;
                        _AddToNameIDDict(name, compoundRefId);
                        m_idInfoDict[compoundRefId] = new IndexItem(compoundChild.Value, compound.GetAttribute("kind", ""), compoundRefId);
                    }
                    else if (compoundChild.Name == "member")
                    {
                        // list members

                        var member = compoundChild;
                        // build member -> compound dict
                        var memberRefId = member.GetAttribute("refid", "");
                        m_idToCompoundDict[memberRefId] = compoundRefId;
                        refIdList.Add(memberRefId);

                        // record name attr
                        var memberChildIter = member.SelectChildren(XPathNodeType.Element);
                        while (memberChildIter.MoveNext())
                        {
                            var memberChild = memberChildIter.Current;
                            if (memberChild.Name == "name")
                            {
                                string name = memberChild.Value;
                                _AddToNameIDDict(name, memberRefId);
                                m_idInfoDict[memberRefId] = new IndexItem(name, member.GetAttribute("kind", ""), memberRefId);
                                break;
                            }
                        }

                    }
                }
                m_compoundToIdDict[compoundRefId] = refIdList;
            }
        }

        bool _ParseRefLocation(XPathNavigator refElement, out string filePath, out int startLine)
        {
            var fileCompoundId = refElement.GetAttribute("compoundref", "");
            if (fileCompoundId == "")
            {
                filePath = "";
                startLine = -1;
                return false;
            }
            filePath = _GetCompoundPath(fileCompoundId);
            startLine = Convert.ToInt32(refElement.GetAttribute("startline", ""));
            return true;
        }

        void _ReadMemberRef(XPathNavigator memberDef, IndexItem compoundItem)
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

            // Add member reference
            string memberFilePath = "";
            int memberStartLine = -1, memberEndLine = -1;
            if (compoundItem != null)
            {
                var compoundId = compoundItem.m_id;
                var memberLocationIter = memberDef.Select("./location");
                if (memberLocationIter.MoveNext())
                {
                    var locationDict = _ParseLocationDict(memberLocationIter.Current);
                    memberFilePath = locationDict["file"].m_string;
                    memberStartLine = locationDict["line"].m_int;
                    memberEndLine = locationDict["lineEnd"].m_int;
                    if (memberFilePath == "")
                    {
                        memberFilePath = locationDict["declFile"].m_string;
                        memberStartLine = locationDict["declLine"].m_int;
                        memberEndLine = memberStartLine;
                    }
                }
            }

            // ref location dict for functions
            var memberRefDict = new Dictionary<string, List<int>>();
            var memberNameRefDict = new Dictionary<string, List<int>>();
            if (memberFilePath != "")
            {
                var fileCompoundId = _GetCompoundIDFromPath(memberFilePath);
                if (fileCompoundId != "")
                {
                    _GetCodeRefs(fileCompoundId, memberStartLine, memberEndLine, out memberRefDict, out memberNameRefDict);
                }
            }
            
            var memberChildIter = memberDef.SelectChildren(XPathNodeType.Element);
            while (memberChildIter.MoveNext())
            {
                var memberChild = memberChildIter.Current;
                if (memberChild.Name == "references")
                {
                    var referenceId = memberChild.GetAttribute("refid", "");

                    // build the reference dict first
                    //if (memberRefDict.Count == 0)
                    //{
                    //    var refElement = _GetXmlElement(referenceId);
                    //    XPathNodeIterator refElementIter = null;
                    //    if (refElement != null)
                    //    {
                    //        refElementIter = refElement.Select(string.Format("./referencedby[@refid=\'{0}\']", memberId));
                    //    }
                    //    if (refElementIter != null && refElementIter.MoveNext())
                    //    {
                    //        refElement = refElementIter.Current;
                    //        var fileCompoundId = refElement.GetAttribute("compoundref", "");
                    //        memberFilePath = _GetCompoundPath(fileCompoundId);
                    //        startLine = Convert.ToInt32(refElement.GetAttribute("startline", ""));
                    //        endLine = Convert.ToInt32(refElement.GetAttribute("endline", ""));
                    //        _GetCodeRefs(fileCompoundId, startLine, endLine, out memberRefDict, out memberNameRefDict);
                    //    }
                    //}

                    if (m_idInfoDict.ContainsKey(referenceId))
                    {
                        var referenceItem = m_idInfoDict[referenceId];
                        string filePathRef;
                        int s;
                        _ParseRefLocation(memberChild, out filePathRef, out s);
                        int startLineRef = memberStartLine;
                        if (memberRefDict.ContainsKey(referenceId))
                        {
                            startLineRef = memberRefDict[referenceId][0];
                        }
                        else if (memberNameRefDict.ContainsKey(referenceItem.m_name))
                        {
                            startLineRef = memberNameRefDict[referenceItem.m_name][0];
                        }
                        var refItem = new IndexRefItem(memberId, referenceId, "unknown", memberFilePath, startLineRef);
                        memberItem.AddRefItem(refItem);
                        referenceItem.AddRefItem(refItem);
                    }
                }

                if (memberChild.Name == "referencedby")
                {
                    var referenceId = memberChild.GetAttribute("refid", "");
                    if (m_idInfoDict.ContainsKey(referenceId))
                    {
                        var referenceItem = m_idInfoDict[referenceId];
                        string filePathRef;
                        int startLineRef;
                        _ParseRefLocation(memberChild, out filePathRef, out startLineRef);

                        // find the actual position in caller's function body
                        if (referenceItem.m_kind == EntKind.FUNCTION ||
                            referenceItem.m_kind == EntKind.SLOT)
                        {
                            var fileCompoundId = memberChild.GetAttribute("compoundref", "");
                            var endLineAttr = memberChild.GetAttribute("endline", "");
                            int endLineRef = endLineAttr == "" ? -1 : Convert.ToInt32(endLineAttr);

                            var memberRefByDict = new Dictionary<string, List<int>>();
                            var memberNameRefByDict = new Dictionary<string, List<int>>();
                            _GetCodeRefs(fileCompoundId, startLineRef, endLineRef, out memberRefByDict, out memberNameRefByDict);
                            if (memberRefByDict.ContainsKey(memberId))
                            {
                                startLineRef = memberRefByDict[memberId][0];
                            }
                            else if (memberNameRefByDict.ContainsKey(memberItem.m_name))
                            {
                                startLineRef = memberNameRefByDict[memberItem.m_name][0];
                            }
                        }
                        var refItem = new IndexRefItem(referenceId, memberId, "unknown", filePathRef, startLineRef);
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
                        string filePathRef;
                        int startLineRef;
                        _ParseRefLocation(memberChild, out filePathRef, out startLineRef);
                        var refItem = new IndexRefItem(memberId, overrideId, "overrides", filePathRef, startLineRef);
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
                        string filePathRef;
                        int startLineRef;
                        _ParseRefLocation(memberChild, out filePathRef, out startLineRef);
                        var refItem = new IndexRefItem(memberId, memberId, "overrides", filePathRef, startLineRef);
                        interfaceItem.AddRefItem(refItem);
                        memberItem.AddRefItem(refItem);
                    }
                }
            }
        }

        void _ReadRef(string uniqueName, int refKind = (int)RefKind.ALL)
        {
            // Find symbol index item
            IndexItem indexItem;
            if (!m_idInfoDict.TryGetValue(uniqueName, out indexItem))
            {
                return;
            }
            
            if (indexItem.GetCacheStatus(IndexItem.CacheStatus.REF))
            {
                return;
            }

            // Find compound index item
            var compoundFileId = uniqueName;
            bool isMember = _IsMember(uniqueName);
            if (isMember)
            {
                compoundFileId = m_idToCompoundDict[uniqueName];
            }
            IndexItem compoundIndexItem;
            if (!m_idInfoDict.TryGetValue(compoundFileId, out compoundIndexItem))
            {
                return;
            }

            // Find xml document
            var doc = _GetXmlDocument(compoundFileId);
            if (doc == null)
            {
                return;
            }

            //var xmlDocItem = _GetXmlDocumentItem(compoundFileId);

            // Parse compound if necessary
            if (!compoundIndexItem.GetCacheStatus(IndexItem.CacheStatus.REF))
            {
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
                            var baseCompoundId = compoundChild.GetAttribute("refid", "");
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

                        // find including files
                        if (compoundChild.Name == "includes")
                        {
                            var includedId = compoundChild.GetAttribute("refid", "");
                            if (m_idInfoDict.ContainsKey(includedId))
                            {
                                var includedItem = m_idInfoDict[includedId];
                                //_ParseRefLocation(compoundChild, out filePath, out startLine);
                                filePath = "";
                                startLine = 0;
                                var refItem = new IndexRefItem(includedId, compoundId, "include", filePath, startLine);
                                includedItem.AddRefItem(refItem);
                                compoundItem.AddRefItem(refItem);
                            }
                        }

                        // find included files
                        if (compoundChild.Name == "includedby")
                        {
                            var includingId = compoundChild.GetAttribute("refid", "");
                            if (m_idInfoDict.ContainsKey(includingId))
                            {
                                var includingItem = m_idInfoDict[includingId];
                                //_ParseRefLocation(compoundChild, out filePath, out startLine);
                                filePath = "";
                                startLine = 0;
                                var refItem = new IndexRefItem(compoundId, includingId, "include", filePath, startLine);
                                includingItem.AddRefItem(refItem);
                                compoundItem.AddRefItem(refItem);
                            }
                        }

                        // find inner members
                        if (compoundChild.Name == "innerclass" || compoundChild.Name == "innernamespace"
                            || compoundChild.Name == "innerfile" || compoundChild.Name == "innerdir")
                        {
                            var innerId = compoundChild.GetAttribute("refid", "");
                            if (m_idInfoDict.ContainsKey(innerId))
                            {
                                var innerItem = m_idInfoDict[innerId];
                                //_ParseRefLocation(compoundChild, out filePath, out startLine);
                                filePath = "";
                                startLine = 0;
                                var refItem = new IndexRefItem(compoundId, innerId, "member", filePath, startLine);
                                innerItem.AddRefItem(refItem);
                                compoundItem.AddRefItem(refItem);
                            }
                        }

                        // find member's refs
                        if (compoundChild.Name == "sectiondef")
                        {
                            var sectionIter = compoundChild.SelectChildren(XPathNodeType.Element);
                            while (sectionIter.MoveNext())
                            {
                                var memberDef = sectionIter.Current;
                                if (memberDef.Name == "memberdef")
                                {
                                    string memberFilePath = "";
                                    int memberStartLine = -1, memberEndLine = -1;
                                    var memberId = memberDef.GetAttribute("id", "");
                                    var memberItem = m_idInfoDict[memberId];
                                    if (compoundItem != null)
                                    {
                                        var memberLocationIter = memberDef.Select("./location");
                                        if (memberLocationIter.MoveNext())
                                        {
                                            var locationDict = _ParseLocationDict(memberLocationIter.Current);
                                            memberFilePath = locationDict["file"].m_string;
                                            memberStartLine = locationDict["line"].m_int;
                                            memberEndLine = locationDict["lineEnd"].m_int;
                                            if (memberFilePath == "")
                                            {
                                                memberFilePath = locationDict["declFile"].m_string;
                                                memberStartLine = locationDict["declLine"].m_int;
                                                memberEndLine = memberStartLine;
                                            }
                                            var refItem = new IndexRefItem(compoundId, memberId, "member", memberFilePath, memberStartLine);
                                            memberItem.AddRefItem(refItem);
                                            compoundItem.AddRefItem(refItem);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                compoundIndexItem.SetCacheStatus(IndexItem.CacheStatus.REF);
            }

            int memberRelation = (int)RefKind.MEMBER | (int)RefKind.DECLARE | (int)RefKind.DEFINE;
            bool shouldParseMember = (refKind & ~memberRelation) != 0;
            // find member's refs
            if (isMember && shouldParseMember)
            {
                var memberIter = doc.Select(string.Format("doxygen/compounddef/sectiondef/memberdef[@id=\'{0}\']", indexItem.m_id));
                while (memberIter.MoveNext())
                {
                    var member = memberIter.Current;
                    _ReadMemberRef(member, compoundIndexItem);
                }
                indexItem.SetCacheStatus(IndexItem.CacheStatus.REF);
            }
        }

        void _ReadRefs()
        {
            if (m_dbFolder == "")
            {
                return;
            }

            foreach (var compoundItem in m_compoundToIdDict)
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

            var cachedElement = XmlDocItem.GetElementRef(refid);
            if (cachedElement != null)
            {
                return cachedElement;
            }

            if (m_idToCompoundDict.ContainsKey(refid))
            {
                var fileName = m_idToCompoundDict[refid];
                XmlDocItem docItem = _GetXmlDocumentItem(fileName);
                var doc = docItem.GetNavigator();
                var memberIter = doc.Select(string.Format("doxygen/compounddef/sectiondef/memberdef[@id=\'{0}\']", refid));
                while (memberIter.MoveNext())
                {
                    var member = memberIter.Current;
                    docItem.AddElementRef(refid, member);
                    return member;
                }
            }
            else if (m_compoundToIdDict.ContainsKey(refid))
            {
                XmlDocItem docItem = _GetXmlDocumentItem(refid);
                var doc = docItem.GetNavigator();
                var compoundIter = doc.Select(string.Format("doxygen/compounddef[@id=\'{0}\']", refid));
                while (compoundIter.MoveNext())
                {
                    var compound = compoundIter.Current;
                    docItem.AddElementRef(refid, compound);
                    return compound;
                }
            }
            return null;
        }

        Dictionary<string, Variant> _ParseLocationDict(XPathNavigator element)
        {
            var declLineAttr = element.GetAttribute("line", "");
            var declLine = declLineAttr != "" ? Convert.ToInt32(declLineAttr) : 1;

            var declColumnAttr = element.GetAttribute("column", "");
            var declColumn = declColumnAttr != "" ? Convert.ToInt32(declColumnAttr): 1;

            var declFile = element.GetAttribute("file", "");
            if (declFile != "" && !declFile.Contains(":"))
            {
                declFile = m_doxyFileFolder + "/" + declFile;
            }

            var bodyStartAttr = element.GetAttribute("bodystart", "");
            var bodyStart = bodyStartAttr != "" ? Convert.ToInt32(bodyStartAttr) : 1;

            var bodyEndAttr = element.GetAttribute("bodyend", "");
            var bodyEnd = bodyEndAttr != "" ? Convert.ToInt32(bodyEndAttr) : -1;

            var bodyFile = element.GetAttribute("bodyfile", "");
            if (bodyFile != "" && !bodyFile.Contains(":"))
            {
                bodyFile = m_doxyFileFolder + "/" + bodyFile;
            }

            int countLine = Math.Max(bodyEnd - bodyStart + 1, 0);
            if (bodyEnd < 0)
            {
                bodyEnd = bodyStart;
                countLine = 0;
            }

            return new Dictionary<string, Variant> {
                { "file", new Variant(bodyFile) },
                { "line", new Variant(bodyStart) },
                { "lineEnd", new Variant(bodyEnd) },
                { "column", new Variant(0) },
                { "CountLine", new Variant(countLine) },
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
                Dictionary<string, Variant> metric = new Dictionary<string, Variant>();
                var id = element.GetAttribute("id", "");
                var childrenIter = element.SelectChildren(XPathNodeType.Element);
                int lastLineNo = -1;
                int nInnerDir = 0;
                int nInnerFile = 0;
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
                    else if (elementChild.Name == "innerfile")
                    {
                        nInnerFile++;
                    }
                    else if (elementChild.Name == "innerdir")
                    {
                        nInnerDir++;
                    }
                    else if (elementChild.Name == "programlisting")
                    {
                        var lastLineIter = elementChild.Select("./codeline[last()-1]");
                        if (lastLineIter.MoveNext())
                        {
                            var lineEle = lastLineIter.Current;
                            var lineNumberAttr = lineEle.GetAttribute("lineno", "");
                            lastLineNo = Convert.ToInt32(lineNumberAttr);
                        }
                    }
                }
                if (lastLineNo != -1)
                {
                    metric["CountLine"] = new Variant(lastLineNo);
                    metric["file"] = metric["declFile"];
                    metric["lineEnd"] = metric["CountLine"];
                }
                if (kind == "file")
                {
                    longName = metric["declFile"].m_string;
                }
                else if (kind == "dir")
                {
                    metric["nFile"] = new Variant(nInnerFile);
                    metric["nDir"] = new Variant(nInnerDir);
                }
                return new Entity(id, name, longName, kind, metric);
            }
            else if (element.Name == "memberdef")
            {
                var name = "";
                var longName = "";
                var kind = element.GetAttribute("kind", "");
                var virt = element.GetAttribute("virt", "");
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
                var type = "";
                var refList = new List<string>();
                var refByList = new List<string>();
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
                    else if (elementChild.Name == "type")
                    {
                        type = elementChild.Value;
                    }
                    else if (elementChild.Name == "references")
                    {
                        var refId = elementChild.GetAttribute("refid","");
                        refList.Add(refId);
                    }
                    else if (elementChild.Name == "referencedby")
                    {
                        var refId = elementChild.GetAttribute("refid", "");
                        refByList.Add(refId);
                    }
                }
                
                if (kind.Contains("function"))
                {
                    // Ignore return type for function
                    var spaceIdx = longName.LastIndexOf(" ");
                    if (spaceIdx != -1)
                    {
                        longName = longName.Substring(spaceIdx);
                    }

                    // Ignore anonymous_namespace{***} for function
                    int beginIdx = 0;
                    int findIdx;
                    string result = "";
                    while ((findIdx = longName.IndexOf("anonymous_namespace{", beginIdx)) != -1)
                    {
                        result += longName.Substring(beginIdx, findIdx - beginIdx);
                        beginIdx = longName.IndexOf("}::", findIdx);
                        if (beginIdx == -1)
                        {
                            break;
                        }
                        beginIdx += 3;
                    }
                    if (beginIdx > 0 && beginIdx < longName.Length)
                    {
                        result += longName.Substring(beginIdx);
                    }
                    if (result != "")
                    {
                        longName = result;
                    }
                    longName = longName.Trim();

                    // Add caller and callee count
                    int callerCount = 0;
                    int calleeCount = 0;
                    foreach (var item in refList)
                    {
                        IndexItem info;
                        if (!m_idInfoDict.TryGetValue(item, out info))
                        {
                            continue;
                        }
                        if (info.m_kind == EntKind.FUNCTION)
                        {
                            calleeCount++;
                        }
                    }
                    foreach (var item in refByList)
                    {
                        IndexItem info;
                        if (!m_idInfoDict.TryGetValue(item, out info))
                        {
                            continue;
                        }
                        if (info.m_kind == EntKind.FUNCTION)
                        {
                            callerCount++;
                        }
                    }
                    metric["nCaller"] = new Variant(callerCount);
                    metric["nCallee"] = new Variant(calleeCount);
                }
                //if (longName.IndexOf(type) == 0)
                //{
                //    longName = longName.Substring(type.Length).Trim();
                //}
                return new Entity(id, name, longName, kind, metric);
            }
            else
            {
                return null;
            }
        }

        public void Open(string fullPath, bool resolveRefPosition)
        {
            if (m_dbFolder != "")
            {
                Close();
            }
            m_resolveReferencePosition = resolveRefPosition;

            m_doxyFileFolder = System.IO.Path.GetDirectoryName(fullPath);
            m_doxyFileFolder = m_doxyFileFolder.Replace('\\', '/');

            _ReadDoxyfile(fullPath, m_metaDict);
            var dbFolders = fromPathList(m_metaDict["OUTPUT_DIRECTORY"]);
            m_dbFolder = dbFolders[0];
            if (m_dbFolder == "")
            {
                m_dbFolder = m_doxyFileFolder;
            }
            m_dbFolder += "/" + m_metaDict["XML_OUTPUT"][0];
            m_dbFolder = m_dbFolder.Replace('\\', '/');

            _ReadIndex();
            //_ReadRefs();
        }

        public string GetDBPath()
        {
            return m_dbFolder + "/index.xml";
        }

        public bool IsOpen()
        {
            return m_dbFolder != "";
        }

        public void Close()
        {
            m_doxyFileFolder = "";
            m_dbFolder = "";
            m_idToCompoundDict.Clear();
            m_compoundToIdDict.Clear();
            foreach (var itemPair in m_idInfoDict)
            {
                itemPair.Value.ClearRefItem();
            }
            m_idInfoDict.Clear();
            XmlDocItem.ClearElementCache();
            m_xmlCache.Clear();
            m_metaDict.Clear();
            m_blockRefDict.Clear();
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

        static public EntKind NameToKind(string name)
        {
            var nameLower = name.ToLower();
            foreach (var item in IndexItem.s_kindDict)
            {
                if (item.Key.ToLower().Contains(nameLower))
                {
                    return item.Value;
                }
            }
            return EntKind.UNKNOWN;
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

        public void SearchAndFilter(string searchWord, string searchKind, string searchFile, int searchLine, 
            out List<Entity> candidateList, out Entity bestEntity, bool exactMatch)
        {
            bestEntity = null;
            candidateList = new List<Entity>();

            var ents = Search(searchWord, searchKind);
            if (ents.Count == 0)
            {
                return;
            }
            if (ents.Count == 1)
            {
                bestEntity = ents[0];
                candidateList.Add(bestEntity);
                return;
            }

            // Filter name
            foreach (var entity in ents)
            {
                if (exactMatch)
                {
                    if (entity.Name() == searchWord)
                    {
                        candidateList.Add(entity);
                    }
                }
                else
                {
                    if (entity.Longname().Contains(searchWord))
                    {
                        candidateList.Add(entity);
                    }
                }
            }

            if (searchFile != "")
            {
                searchFile = searchFile.Replace("\\","/");
                var entList = candidateList;
                candidateList = new List<Entity>();
                var bestEntDist = new List<int>();
                var searchWordLower = searchWord.ToLower();

                foreach (var ent in entList)
                {
                    var refs = SearchRef(ent.UniqueName());
                    if (refs.Count == 0)
                    {
                        continue;
                    }

                    var fileNameSet = new HashSet<string>();
                    var lineDist = int.MaxValue;
                    foreach (var refObj in refs)
                    {
                        if (refObj == null)
                        {
                            continue;
                        }

                        var fileEnt = refObj.File();
                        var line = refObj.Line();
                        var column = refObj.Column();
                        fileNameSet.Add(fileEnt.Longname());
                        if (fileEnt.Longname().Contains(searchFile))
                        {
                            lineDist = Math.Min(lineDist, Math.Abs(line - searchLine));
                        }
                    }

                    if (searchWordLower.Contains(ent.Name().ToLower()))
                    {
                        candidateList.Add(ent);
                        bestEntDist.Add(lineDist);
                    }
                }

                if (searchLine > -1)
                {
                    var minDist = int.MaxValue;
                    Entity bestEnt = null;
                    for (int i = 0; i < candidateList.Count; i++)
                    {
                        if (bestEntDist[i] < minDist)
                        {
                            minDist = bestEntDist[i];
                            bestEnt = candidateList[i];
                        }
                    }
                    if (bestEnt != null)
                    {
                        bestEntity = bestEnt;
                    }
                }
            }

            // The only possible choice
            if (bestEntity == null && candidateList.Count == 1)
            {
                bestEntity = candidateList[0];
            }
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

        List<Tuple<RefKind, bool>> _GetRefKindList(string refKindStr)
        {
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
                refKindList = new List<Tuple<RefKind, bool>>(IndexRefItem.s_kindDict.Values);
            }
            return refKindList;
        }

        List<EntKind> _GetEntityKindList(string entKindStr)
        {
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
            return entKindList;
        }

        void _SearchRef(out List<Entity> refEntityList, out List<Reference> refRefList, string uniqueName, string refKindStr = "", string entKindStr = "", bool isUnique = true)
        {
            refEntityList = new List<Entity>();
            refRefList = new List<Reference>();
            if (!m_idInfoDict.ContainsKey(uniqueName))
            {
                return;
            }
            var thisItem = m_idInfoDict[uniqueName];

            // parse refKindStr
            var refKindList = _GetRefKindList(refKindStr);
            int refKindBit = 0;
            foreach (var item in refKindList)
            {
                refKindBit |= (int)item.Item1;
            }

            // parse entKindStr
            var entKindList = _GetEntityKindList(entKindStr);

            // build reference link
            var compoundId = uniqueName;
            if (m_idToCompoundDict.ContainsKey(uniqueName))
            {
                compoundId = m_idToCompoundDict[uniqueName];
            }
            _ReadRef(uniqueName, refKindBit);

            var thisEntity = SearchFromUniqueName(uniqueName);
            if (thisItem.m_kind == EntKind.FILE || thisItem.m_kind == EntKind.DIR)
            {
                var location = thisEntity.Longname();
                var dirIdx = location.LastIndexOf("/");
                if (dirIdx != -1)
                {
                    var dirName = location.Substring(0, dirIdx);
                    var ents = Search(dirName, "dir");
                    foreach (var entity in ents)
                    {
                        if (entity.m_longName == dirName)
                        {
                            _ReadRef(entity.m_id);
                            break;
                        }
                    }
                }
            }

            // find references
            var refs = thisItem.GetRefItemList();
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
                    else if (refKind == RefKind.USE && (refObj.m_kind == RefKind.UNKNOWN || refObj.m_kind == RefKind.USE))
                    {
                        if (srcItem.m_kind == EntKind.FUNCTION && dstItem.m_kind == EntKind.VARIABLE)
                        {
                            isAccepted = true;
                        }
                        else if (srcItem.m_kind == EntKind.FILE && dstItem.m_kind == EntKind.FILE)
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
