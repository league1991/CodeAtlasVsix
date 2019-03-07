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
    // This class is used to find the cursor using the given CodeUIItem, or 
    // find the CodeUIItem using the given cursor.
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

            m_dte = Package.GetGlobalService(typeof(DTE)) as DTE2;

            var t1 = DateTime.Now;
            //Logger.Debug("---CursorNavigator::CursorNavigator" + (t1 - t0).TotalMilliseconds.ToString());
        }

        static bool UseCodeModel()
        {
            return !DBManager.Instance().IsBigSolution() &&
                UIManager.Instance().GetMainUI().IsDynamicNavigation();
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
                        var endPnt = codeElement.EndPoint;

                        TextSelection ts = document.Selection as TextSelection;
                        if (ts != null && ts.TopPoint.GreaterThan(startPnt) && ts.BottomPoint.LessThan(endPnt))
                        {
                            startPnt = ts.TopPoint;
                        }
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

        static public void GetCursorPosition(out string path, out int line, out int column)
        {
            path = "";
            line = column = -1;
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            Document doc = null;
            if (dte != null)
            {
                doc = dte.ActiveDocument;
            }
            if (doc == null)
            {
                return;
            }
            path = doc.FullName;
            EnvDTE.TextSelection ts = doc.Selection as EnvDTE.TextSelection;
            if (ts != null)
            {
                line = ts.ActivePoint.Line;
                column = ts.ActivePoint.DisplayColumn;
            }
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
            string searchToken = "";

            try
            {
                if (codeItem != null)
                {
                    codeItem.GetDefinitionPosition(out fileName, out line, out column);
                    if (codeItem.GetKind() == DoxygenDB.EntKind.PAGE)
                    {
                        MoveTo(fileName, line, column);
                        return;
                    }
                    if (codeItem.GetKind() == DoxygenDB.EntKind.FILE)
                    {
                        OpenFile(fileName);
                        return;
                    }
                    res = ShowItemDefinition(codeItem, fileName);
                    searchToken = codeItem.GetName();
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
                    searchToken = tarItem.GetName();

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
                    else if (srcItem.GetKind() == DoxygenDB.EntKind.PAGE)
                    {
                        srcItem.GetDefinitionPosition(out fileName, out line, out column);
                        MoveTo(fileName, line, column);
                        return;
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
                        TextDocument textDoc = ts.Parent;
                        if (ts != null && line > 0 && textDoc != null)
                        {
                            var formatStr = string.Format(@"\b{0}\b", searchToken);
                            int beginLine = Math.Max(1, line - 30);
                            int endLine = line + 31;
                            int endOffset = 1;
                            if (endLine > textDoc.EndPoint.Line)
                            {
                                endLine = textDoc.EndPoint.Line;
                                endOffset = textDoc.EndPoint.LineLength+1;
                            }
                            ts.MoveToLineAndOffset(beginLine, 1, false);
                            ts.MoveToLineAndOffset(endLine, endOffset, true);
                            string searchText = ts.Text.Replace("\r\n", "\n");
                            int lineBeginIdx = 0;
                            int bestLine = int.MaxValue, bestColumn = -1;
                            for (int currentLine = beginLine; ;currentLine++)
                            {
                                int lineEndIdx = searchText.IndexOf("\n", lineBeginIdx);
                                if (lineEndIdx == -1)
                                {
                                    lineEndIdx = searchText.Length;
                                }
                                string lineStr = searchText.Substring(lineBeginIdx, lineEndIdx - lineBeginIdx);
                                var matches = Regex.Matches(lineStr, formatStr, RegexOptions.ExplicitCapture);
                                foreach (Match nextMatch in matches)
                                {
                                    if (Math.Abs(currentLine-line) < Math.Abs(bestLine-line))
                                    {
                                        bestLine = currentLine;
                                        bestColumn = nextMatch.Index + 1;
                                    }
                                }
                                if (lineEndIdx == -1 || lineEndIdx >= searchText.Length-1)
                                {
                                    break;
                                }
                                lineBeginIdx = lineEndIdx + 1;
                            }
                            if (bestColumn != -1)
                            {
                                ts.MoveTo(bestLine, bestColumn);
                                res = true;
                            }

                            if (res == false)
                            {
                                ts.MoveTo(line, column);
                                //ts.GotoLine(line);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Logger.Debug("Go to page fail.");
                    }
                }
            }
        }

        bool TryToMoveTo(string fileName, int line, string searchToken)
        {
            if (!File.Exists(fileName))
                return false;

            try
            {
                OpenFile(fileName);
                TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
                TextDocument textDoc = ts.Parent;
                if (ts != null && textDoc != null && line >= textDoc.StartPoint.Line && line <= textDoc.EndPoint.Line)
                {
                    ts.GotoLine(line, true);
                    var lineStr = ts.Text;
                    var formatStr = string.Format(@"\b{0}\b", searchToken);
                    var matches = Regex.Matches(lineStr, formatStr, RegexOptions.ExplicitCapture);
                    int column = -1;
                    foreach (Match nextMatch in matches)
                    {
                        column = nextMatch.Index + 1;
                        break;
                    }
                    if (column == -1)
                    {
                        return false;
                    }
                    ts.MoveToLineAndOffset(line, column, false);
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        void MoveTo(string fileName, int line, int column)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    OpenFile(fileName);
                    TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
                    TextDocument textDoc = ts.Parent;
                    if (line > textDoc.EndPoint.Line)
                    {
                        line = textDoc.EndPoint.Line;
                        column = 1;
                    }
                    if (ts != null && line > 0 && textDoc != null)
                    {
                        ts.MoveTo(line, column);
                    }
                }
                catch (Exception)
                {
                    Logger.Debug("Go to page fail.");
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

        public void OpenFile(string fileName)
        {
            var filePath = fileName.Replace('/', '\\');
            if (m_dte.get_IsOpenFile(EnvDTE.Constants.vsViewKindCode, filePath))
            {
                Document doc = m_dte.Documents.Item(filePath);
                doc.Activate();
                return;
            }

            Window window = null;
            if(m_dte.Solution != null)
            {
                var projItem = m_dte.Solution.FindProjectItem(filePath);
                if (projItem != null && !projItem.IsOpen)
                {
                    window = projItem.Open();
                }
            }
            //foreach (Window win in m_dte.Documents.Cast<Document>()
            //                     .FirstOrDefault(s => s.FullName == filePath).Windows)
            //    win.Close();
            if (window == null)
            {
                window = m_dte.ItemOperations.OpenFile(fileName, EnvDTE.Constants.vsViewKindCode);
            }
            if (window != null)
            {
                window.Visible = true;
                window.Activate();
            }
        }

        static FileCodeModel GetFileCodeModel(Document document)
        {
            if (document == null || UseCodeModel() == false)
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
            if (vcDocModel != null)
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

        static public void MoveToLindEnd()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte != null)
            {
                var document = dte.ActiveDocument;
                if (document == null)
                {
                    return;
                }

                EnvDTE.TextSelection ts = document.Selection as EnvDTE.TextSelection;
                ts.EndOfLine();
            }
        }
        static public void GetCursorElement(out Document document, out CodeElement element, out int line)
        {
            document = null;
            element = null;
            line = -1;
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte != null)
            {
                document = dte.ActiveDocument;
                //var docItem = document.ProjectItem;
                //if (docItem == null)
                //{
                //    return;
                //}
                var docModel = GetFileCodeModel(document);
                if (docModel == null)
                {
                    return;
                }
                EnvDTE.TextSelection ts = document.Selection as EnvDTE.TextSelection;
                line = ts.CurrentLine;
                try
                {
                    element = docModel.CodeElementFromPoint(ts.ActivePoint, vsCMElement.vsCMElementFunction);
                }
                catch (Exception e)
                {
                    element = null;
                }
                return;
            }
            return;
        }
        
        static public string VSElementTypeToString(CodeElement element)
        {
            string typeStr = "";
            var type = element.Kind;
            switch (type)
            {
                case vsCMElement.vsCMElementOther:
                    break;
                case vsCMElement.vsCMElementClass:
                    typeStr = "class";
                    break;
                case vsCMElement.vsCMElementFunction:
                    typeStr = "function";
                    break;
                case vsCMElement.vsCMElementVariable:
                    typeStr = "variable";
                    break;
                case vsCMElement.vsCMElementProperty:
                    break;
                case vsCMElement.vsCMElementNamespace:
                    typeStr = "namespace";
                    break;
                case vsCMElement.vsCMElementParameter:
                    break;
                case vsCMElement.vsCMElementAttribute:
                    break;
                case vsCMElement.vsCMElementInterface:
                    typeStr = "function";
                    break;
                case vsCMElement.vsCMElementDelegate:
                    break;
                case vsCMElement.vsCMElementEnum:
                    break;
                case vsCMElement.vsCMElementStruct:
                    typeStr = "struct";
                    break;
                case vsCMElement.vsCMElementUnion:
                    break;
                case vsCMElement.vsCMElementLocalDeclStmt:
                    break;
                case vsCMElement.vsCMElementFunctionInvokeStmt:
                    break;
                case vsCMElement.vsCMElementPropertySetStmt:
                    break;
                case vsCMElement.vsCMElementAssignmentStmt:
                    break;
                case vsCMElement.vsCMElementInheritsStmt:
                    break;
                case vsCMElement.vsCMElementImplementsStmt:
                    break;
                case vsCMElement.vsCMElementOptionStmt:
                    break;
                case vsCMElement.vsCMElementVBAttributeStmt:
                    break;
                case vsCMElement.vsCMElementVBAttributeGroup:
                    break;
                case vsCMElement.vsCMElementEventsDeclaration:
                    break;
                case vsCMElement.vsCMElementUDTDecl:
                    break;
                case vsCMElement.vsCMElementDeclareDecl:
                    break;
                case vsCMElement.vsCMElementDefineStmt:
                    break;
                case vsCMElement.vsCMElementTypeDef:
                    break;
                case vsCMElement.vsCMElementIncludeStmt:
                    break;
                case vsCMElement.vsCMElementUsingStmt:
                    break;
                case vsCMElement.vsCMElementMacro:
                    break;
                case vsCMElement.vsCMElementMap:
                    break;
                case vsCMElement.vsCMElementIDLImport:
                    break;
                case vsCMElement.vsCMElementIDLImportLib:
                    break;
                case vsCMElement.vsCMElementIDLCoClass:
                    break;
                case vsCMElement.vsCMElementIDLLibrary:
                    break;
                case vsCMElement.vsCMElementImportStmt:
                    break;
                case vsCMElement.vsCMElementMapEntry:
                    break;
                case vsCMElement.vsCMElementVCBase:
                    break;
                case vsCMElement.vsCMElementEvent:
                    break;
                case vsCMElement.vsCMElementModule:
                    break;
                default:
                    break;
            }
            return typeStr;
        }

        static public void GetCursorWord(EnvDTE.TextSelection ts, out string name, out string longName, out int lineNum)
        {
            name = null;
            longName = null;
            lineNum = ts.AnchorPoint.Line;
            int lineOffset = ts.AnchorPoint.LineCharOffset;

            // If a line of text is selected, used as search word.
            var cursorStr = ts.Text.Trim();
            if (cursorStr.Length != 0 && cursorStr.IndexOf('\n') == -1)
            {
                name = cursorStr;
                longName = cursorStr;
                return;
            }

            // Otherwise, use the word under the cursor.
            ts.SelectLine();
            string lineText = ts.Text;
            ts.MoveToLineAndOffset(lineNum, lineOffset);

            Regex rx = new Regex(@"\b(?<word>\w+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            MatchCollection matches = rx.Matches(lineText);

            // Report on each match
            int lastStartIndex = 0;
            int lastEndIndex = 0;
            for (int ithMatch = 0; ithMatch < matches.Count; ++ithMatch)
            {
                var match = matches[ithMatch];
                string word = match.Groups["word"].Value;
                int startIndex = match.Groups["word"].Index;
                int endIndex = startIndex + word.Length;
                int lineIndex = lineOffset - 1;
                if (startIndex <= lineIndex && endIndex >= lineIndex)
                {
                    name = word;
                    longName = word;
                    var midStr = lineText.Substring(lastEndIndex, (startIndex - lastEndIndex));
                    if (ithMatch > 0 && midStr == "::")
                    {
                        longName = lineText.Substring(lastStartIndex, (endIndex - lastStartIndex));
                        break;
                    }
                }
                lastStartIndex = startIndex;
                lastEndIndex = endIndex;
            }
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
