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
            m_dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
        }

        static bool IsMatchPair(DoxygenDB.EntKind doxyKind, vsCMElement vsKind)
        {
            int idx = s_typeMap.FindIndex(delegate (KindPair p) { return p.m_doxyKind == doxyKind && p.m_vsKind == vsKind; });
            return idx != -1;
        }

        public bool CheckAndMoveTo(TextPoint pnt, Document doc)
        {
            if (pnt.Parent.Parent == doc)
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
                m_dte.ItemOperations.OpenFile(fileName);
                var document = m_dte.ActiveDocument;
                var codeElement = GetCodeElement(codeItem, document);
                if (codeElement != null)
                {
                    try
                    {
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
                        m_dte.ItemOperations.OpenFile(fileName);
                        var document = m_dte.ActiveDocument;
                        var codeElement = GetCodeElement(srcItem, document);
                        if (codeElement != null)
                        {
                            var funcStart = codeElement.GetStartPoint(vsCMPart.vsCMPartBody);
                            var funcEnd = codeElement.GetEndPoint(vsCMPart.vsCMPartBody);
                            var funcEditPnt = funcStart.CreateEditPoint();
                            string funcText = funcEditPnt.GetText(funcEnd);
                            
                            string indentifierPattern = string.Format(@"\b{0}\b", tarItem.GetName());

                            try
                            {
                                var nameList = Regex.Matches(funcText, indentifierPattern, RegexOptions.ExplicitCapture);

                                foreach (Match nextMatch in nameList)
                                {
                                    var tokenValue = nextMatch.Value;
                                    var tokenIndex = nextMatch.Index;
                                    var prevText = funcText.Substring(0, nextMatch.Index);
                                    var nReturnChar = prevText.Length - prevText.Replace("\r\n", "\n").Length;
                                    var absOffset = funcStart.AbsoluteCharOffset + tokenIndex - nReturnChar;

                                    TextSelection ts = document.Selection as TextSelection;
                                    ts.MoveToAbsoluteOffset(absOffset);
                                    res = true;
                                    break;
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
                else
                {
                    res = ShowItemDefinition(tarItem, fileName);
                }
            }

            //Logger.WriteLine(string.Format("show in editor:{0} {1}", fileName, line));

            if (res == false)
            {
                if (File.Exists(fileName))
                {
                    m_dte.ItemOperations.OpenFile(fileName);
                    TextSelection ts = m_dte.ActiveDocument.Selection as TextSelection;
                    if (ts != null && line > 0)
                    {
                        try
                        {
                            ts.GotoLine(line);
                        }
                        catch (Exception)
                        {
                            Logger.WriteLine("Go to page fail.");
                        }
                    }
                }
            }
        }

        CodeElement GetCodeElement(CodeUIItem uiItem, Document document)
        {
            var docItem = document.ProjectItem;
            var docModel = docItem.FileCodeModel;
            if (docModel == null)
            {
                return null;
            }

            var vcDocModel = docModel as VCFileCodeModel;
            if (vcDocModel != null)
            {
                var candidates = vcDocModel.CodeElementFromFullName(uiItem.GetLongName());
                if (candidates != null)
                {
                    foreach (var item in candidates)
                    {
                        var candidate = item as CodeElement;
                        if (candidate != null)
                        {
                            return candidate;
                        }
                    }
                }
            }

            var elements = docModel.CodeElements;
            // TraverseCodeElement(elements, 1);
            var elementsList = new List<CodeElements> { docModel.CodeElements };
            while (elementsList.Count > 0)
            {
                var curElements = elementsList[0];
                elementsList.RemoveAt(0);

                var elementIter = curElements.GetEnumerator();
                while (elementIter.MoveNext())
                {
                    var element = elementIter.Current as CodeElement;
                    var eleName = element.Name;
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
                    
                    if (eleName  == uiItem.GetName())
                    {
                        return element;
                    }

                    var children = element.Children;
                    if (children != null)
                    {
                        elementsList.Add(children);
                    }
                }
            }
            return null;
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
                Logger.WriteLine(indentStr + string.Format("element:{0} {1} {2}", eleName, startLine, endLine));
                

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
