using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.VCCodeModel;

namespace CodeAtlasVSIX
{
    class CursorNavigator
    {
        struct KindPair
        {
            public KindPair(DoxygenDB.EntKind doxyKind, vsCMElement vsKind)
            {
                m_doxyKind = doxyKind;
                m_vsKind = vsKind;
            }
            public DoxygenDB.EntKind m_doxyKind;
            public vsCMElement m_vsKind;
        };

        DTE2 m_dte;
        bool m_useCodeModel = true;

        static List<KindPair> s_typeMap =
            new List<KindPair>
        {
            new KindPair(DoxygenDB.EntKind.CLASS, vsCMElement.vsCMElementClass),
            new KindPair(DoxygenDB.EntKind.DEFINE, vsCMElement.vsCMElementMacro),
            new KindPair(DoxygenDB.EntKind.ENUM, vsCMElement.vsCMElementEnum),
            new KindPair(DoxygenDB.EntKind.FUNCTION, vsCMElement.vsCMElementFunction),
            new KindPair(DoxygenDB.EntKind.INTERFACE, vsCMElement.vsCMElementInterface),
            new KindPair(DoxygenDB.EntKind.NAMESPACE, vsCMElement.vsCMElementNamespace),
            new KindPair(DoxygenDB.EntKind.SIGNAL, vsCMElement.vsCMElementFunction),
            new KindPair(DoxygenDB.EntKind.SLOT, vsCMElement.vsCMElementFunction),
            new KindPair(DoxygenDB.EntKind.STRUCT, vsCMElement.vsCMElementStruct),
            new KindPair(DoxygenDB.EntKind.TYPEDEF, vsCMElement.vsCMElementTypeDef),
            new KindPair(DoxygenDB.EntKind.UNION, vsCMElement.vsCMElementUnion),
            new KindPair(DoxygenDB.EntKind.VARIABLE, vsCMElement.vsCMElementVariable),
        };

        public CursorNavigator()
        {
            var t0 = DateTime.Now;
            m_useCodeModel = !DBManager.Instance().IsBigSolution() && 
                UIManager.Instance().GetMainUI().IsDynamicNavigation();

            m_dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            var t1 = DateTime.Now;
            Logger.Debug("---CursorNavigator::CursorNavigator" + (t1 - t0).TotalMilliseconds.ToString());
        }

        static bool IsMatchPair(DoxygenDB.EntKind doxyKind, vsCMElement vsKind)
        {
            int idx = s_typeMap.FindIndex(delegate (KindPair p) { return p.m_doxyKind == doxyKind && p.m_vsKind == vsKind; });
            return idx != -1;
        }

        public bool CheckAndMoveTo(TextPoint pnt, Document doc)
        {
            if (pnt.Parent != null && pnt.Parent.Parent == doc)
            {
                TextSelection ts = doc.Selection as TextSelection;
                ts.MoveToPoint(pnt);
                return true;
            }
            return false;
        }

        public bool ShowItemDefinition(CodeUIItem codeItem, string fileName)
        {
            if (File.Exists(fileName))
            {
                OpenFile(fileName);

                var document = m_dte.ActiveDocument;
                var codeElement = GetCodeElement(codeItem, document);

                if (codeElement != null)
                {
                    try
                    {
                        UpdateBodyCode(codeItem, codeElement);

                        var startPnt = codeElement.StartPoint;
                        if (CheckAndMoveTo(startPnt, document))
                        {
                            return true;
                        }

                        // In c++, both a function's declaration and definition should be checked
                        var vcCodeElement = codeElement as VCCodeElement;
                        if (vcCodeElement != null)
                        {
                            List<vsCMWhere> whereList = new List<vsCMWhere>
                            {
                                vsCMWhere.vsCMWhereDeclaration,
                                vsCMWhere.vsCMWhereDefinition,
                                vsCMWhere.vsCMWhereDefault
                            };

                            foreach (var where in whereList)
                            {
                                startPnt = vcCodeElement.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, where);

                                if (CheckAndMoveTo(startPnt, document))
                                {
                                    return true;
                                }
                            }
                        }
                        return false;
                    }
                    catch (Exception e)
                    {
                        var msg = e.Message;
                    }
                }
            }
            return false;
        }

        string UpdateBodyCode(CodeUIItem item, CodeElement codeElement)
        {
            if (codeElement == null || item == null || !item.IsFunction())
            {
                return "";
            }
            var funcStart = codeElement.GetStartPoint(vsCMPart.vsCMPartBody);
            var funcEnd = codeElement.GetEndPoint(vsCMPart.vsCMPartBody);
            var funcEditPnt = funcStart.CreateEditPoint();
            string funcText = funcEditPnt.GetText(funcEnd).Replace("\r\n", "\n");
            item.m_bodyCode = funcText;
            return funcText;
        }

        public void Navigate(object item)
        {
            if (m_dte == null)
            {
                return;
            }

            var codeItem = item as CodeUIItem;
            var edgeItem = item as CodeUIEdgeItem;
            var fileName = "";
            int line = 0;
            int column = 0;
            bool res = false;

            try
            {
                if (codeItem != null)
                {
                    codeItem.GetDefinitionPosition(out fileName, out line, out column);

                    res = ShowItemDefinition(codeItem, fileName);
                }
                else if (edgeItem != null)
                {
                    line = edgeItem.m_line;
                    column = edgeItem.m_column;
                    fileName = edgeItem.m_file;

                    var scene = UIManager.Instance().GetScene();
                    var itemDict = scene.GetItemDict();
                    var srcItem = itemDict[edgeItem.m_srcUniqueName];
                    var tarItem = itemDict[edgeItem.m_tarUniqueName];

                    if (srcItem.IsFunction())
                    {
                        if (File.Exists(fileName))
                        {
                            OpenFile(fileName);
                            var document = m_dte.ActiveDocument;

                            var codeElement = GetCodeElement(srcItem, document);

                            if (codeElement != null)
                            {
                                var funcStart = codeElement.GetStartPoint(vsCMPart.vsCMPartBody);
                                var funcText = UpdateBodyCode(srcItem, codeElement);

                                int offset = -1;
                                if (tarItem.IsFunction())
                                {
                                    offset = FindOffset(funcText, string.Format(@"\b{0}\(", tarItem.GetName()));
                                }
                                if (offset == -1)
                                {
                                    offset = FindOffset(funcText, string.Format(@"\b{0}\b", tarItem.GetName()));
                                }
                                if (offset != -1)
                                {
                                    var absOffset = funcStart.AbsoluteCharOffset + offset;
                                    TextSelection ts = document.Selection as TextSelection;
                                    ts.MoveToAbsoluteOffset(absOffset);
                                    res = true;
                                }
                            }

                        }
                    }
                    else
                    {
                        res = ShowItemDefinition(tarItem, fileName);
                    }
                }
            }
            catch (Exception)
            {
                res = false;
            }

            if (res == false)
            {
                if (File.Exists(fileName))
                {
                    try
                    {
                        OpenFile(fileName);
                        TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
                        if (ts != null && line > 0)
                        {
                            ts.GotoLine(line);
                        }
                    }
                    catch (Exception)
                    {
                        Logger.Debug("Go to page fail.");
                    }
                }
            }
        }

        int FindOffset(string funcText, string pattern)
        {
            int offset = -1;

            var match = Regex.Match(funcText, pattern, RegexOptions.ExplicitCapture);
            if (match.Success)
            {
                return match.Index;
            }
            return offset;
        }

        void OpenFile(string fileName)
        {
            var filePath = fileName.Replace('/', '\\');
            if (m_dte.get_IsOpenFile(EnvDTE.Constants.vsViewKindCode, filePath))
            {
                Document doc = m_dte.Documents.Item(filePath);
                doc.Activate();
                return;
            }

            //foreach (Window win in m_dte.Documents.Cast<Document>()
            //                     .FirstOrDefault(s => s.FullName == filePath).Windows)
            //    win.Close();

            var window = m_dte.ItemOperations.OpenFile(fileName, EnvDTE.Constants.vsViewKindCode);
            if (window != null)
            {
                window.Visible = true;
                window.Activate();
            }
        }

        FileCodeModel GetFileCodeModel(Document document)
        {
            if (m_useCodeModel == false)
            {
                return null;
            }

            var docItem = document.ProjectItem;
            if (docItem == null)
            {
                return null;
            }
            var docModel = docItem.FileCodeModel;
            return docModel;
        }

        CodeElement GetCodeElement(CodeUIItem uiItem, Document document)
        {
            if (document == null)
            {
                return null;
            }
            var t0 = DateTime.Now;

            var docModel = GetFileCodeModel(document);
            if (docModel == null)
            {
                return null;
            }

            var t1 = DateTime.Now;
            Logger.Debug("GetCodeElement:: get file code model " + (t1 - t0).TotalMilliseconds.ToString());
            t0 = t1;

            var metric = uiItem.GetMetric();
            int uiItemLine = -1;
            var docName = document.Name.ToLower();
            if (metric.ContainsKey("file") && metric["file"].m_string.ToLower().Contains(docName))
            {
                uiItemLine = metric["line"].m_int;
            }
            else if (metric.ContainsKey("declFile") && metric["declFile"].m_string.ToLower().Contains(docName))
            {
                uiItemLine = metric["declLine"].m_int;
            }

            var vcDocModel = docModel as VCFileCodeModel;
            if (vcDocModel != null && false)
            {
                var candidates = vcDocModel.CodeElementFromFullName(uiItem.GetLongName());

                if (candidates != null)
                {
                    var candidateList = new List<Tuple<CodeElement, int>>();
                    foreach (var item in candidates)
                    {
                        var candidate = item as CodeElement;
                        if (candidate != null)
                        {
                            int lineDist = int.MaxValue;
                            var startLine = candidate.StartPoint.Line;
                            var endLine = candidate.EndPoint.Line;
                            if (uiItemLine != -1)
                            {
                                var startDist = Math.Abs(uiItemLine - startLine);
                                var endDist = Math.Abs(uiItemLine - endLine);
                                lineDist = startDist + endDist;
                            }

                            candidateList.Add(new Tuple<CodeElement, int>(candidate, lineDist));
                        }
                    }

                    candidateList.Sort((x, y) => (x.Item2.CompareTo(y.Item2)));
                    if (candidateList.Count != 0)
                    {
                        return candidateList[0].Item1;
                    }
                }
            }

            var elements = docModel.CodeElements;
            // TraverseCodeElement(elements, 1);
            var elementsList = new List<CodeElements> { docModel.CodeElements };
            var nameMatchList = new List<Tuple<CodeElement, bool, int>>();
            while (elementsList.Count > 0)
            {
                var curElements = elementsList[0];
                elementsList.RemoveAt(0);

                var elementIter = curElements.GetEnumerator();
                while (elementIter.MoveNext())
                {
                    var element = elementIter.Current as CodeElement;
                    try
                    {
                        var eleName = element.Name;
                        var eleFullName = element.FullName;
                        var eleStart = element.StartPoint.Line;
                        var eleEnd = element.EndPoint.Line;
                        var lineDist = int.MaxValue;
                        if (uiItemLine != -1)
                        {
                            var startDist = Math.Abs(uiItemLine - eleStart);
                            var endDist = Math.Abs(uiItemLine - eleEnd);
                            lineDist = startDist + endDist;
                        }
                        //var start = element.GetStartPoint(vsCMPart.vsCMPartBody);
                        //var end = element.GetEndPoint(vsCMPart.vsCMPartBody);
                        //var vcElement = element as VCCodeElement;
                        //if (vcElement != null)
                        //{
                        //    start = vcElement.get_StartPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
                        //    end = vcElement.get_EndPointOf(vsCMPart.vsCMPartWholeWithAttributes, vsCMWhere.vsCMWhereDeclaration);
                        //}
                        //var startLine = start.Line;
                        //var endLine = end.Line;
                        //string indentStr = "";
                        //var itemDoc = start.Parent.Parent;
                        //var itemPath = itemDoc.Name;
                        //var docPath = document.Name;
                        //Logger.WriteLine(indentStr + string.Format("element:{0} {1} {2}", eleName, itemPath, startLine));
                        //Logger.WriteLine("-----" + eleName);
                        if (eleFullName != null && uiItem.GetLongName().Contains(eleFullName) &&
                            IsMatchPair(uiItem.GetKind(), element.Kind))
                        {
                            nameMatchList.Add(new Tuple<CodeElement, bool, int>(element, true, lineDist));
                        }
                        else if (eleName == uiItem.GetName())
                        {
                            nameMatchList.Add(new Tuple<CodeElement, bool, int>(element, false, lineDist));
                        }

                        var children = element.Children;
                        if (children != null)
                        {
                            elementsList.Add(children);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            nameMatchList.Sort((x, y) => (
                (x.Item2 == y.Item2) ? (x.Item3.CompareTo(y.Item3)) : (x.Item2 == true ? -1 : 1)
            ));
            if (nameMatchList.Count > 0)
            {
                return nameMatchList[0].Item1;
            }
            return null;
        }

        public void GetCursorElement(out Document document, out CodeElement element, out int line)
        {
            document = null;
            element = null;
            line = -1;
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte != null)
            {
                document = dte.ActiveDocument;
                var docItem = document.ProjectItem;
                if (docItem == null)
                {
                    return;
                }
                var docModel = GetFileCodeModel(document);
                if (docModel == null)
                {
                    return;
                }
                EnvDTE.TextSelection ts = document.Selection as EnvDTE.TextSelection;
                line = ts.CurrentLine;
                element = docModel.CodeElementFromPoint(ts.ActivePoint, vsCMElement.vsCMElementFunction);
                return;
            }
            return;
        }

        void TraverseCodeElement(CodeElements elements, int indent)
        {
            var elementIter = elements.GetEnumerator();
            while (elementIter.MoveNext())
            {
                var element = elementIter.Current as CodeElement;
                if (element == null)
                {
                    continue;
                }
                var eleName = element.Name;
                var start = element.StartPoint;
                var end = element.EndPoint;
                var startLine = start.Line;
                var endLine = end.Line;
                string indentStr = "";
                for (int i = 0; i < indent; i++)
                {
                    indentStr += "\t";
                }
                Logger.Debug(indentStr + string.Format("element:{0} {1} {2}", eleName, startLine, endLine));
                

                var children = element.Children;
                if (children != null)
                {
                    TraverseCodeElement(children, indent + 1);
                }

                //var kind = element.Kind;
                //var name = element.Name;
                //if (element.Kind == vsCMElement.vsCMElementNamespace)
                //{
                //    var ns = element as CodeNamespace;
                //    TraverseCodeElement(ns.Members, indent + 1);
                //}
            }
        }
    }
}
