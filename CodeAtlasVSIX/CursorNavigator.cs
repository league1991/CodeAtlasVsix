using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    class CursorNavigator
    {
        DTE2 m_dte;

        public CursorNavigator()
        {
            m_dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
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
            if (codeItem != null)
            {
                codeItem.GetDefinitionPosition(out fileName, out line, out column);
                if (File.Exists(fileName))
                {
                    m_dte.ItemOperations.OpenFile(fileName);
                    var document = m_dte.ActiveDocument;
                    var codeElement = GetCodeElement(codeItem, document);
                    if (codeElement != null)
                    {
                        TextSelection ts = document.Selection as TextSelection;
                        ts.MoveToPoint(codeElement.StartPoint);
                        return;
                    }
                }
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

            Logger.WriteLine(string.Format("show in editor:{0} {1}", fileName, line));

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

        CodeElement GetCodeElement(CodeUIItem uiItem, Document document)
        {
            var docItem = document.ProjectItem;
            var docModel = docItem.FileCodeModel;
            var elements = docModel.CodeElements;
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
                    var start = element.StartPoint;
                    var end = element.EndPoint;
                    var startLine = start.Line;
                    var endLine = end.Line;
                    string indentStr = "";
                    Logger.WriteLine(indentStr + string.Format("element:{0} {1} {2}", eleName, startLine, endLine));
                    if (element.Name == uiItem.GetName())
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

        //void TraverseCodeElement(CodeElements elements, int indent)
        //{
        //    var elementIter = elements.GetEnumerator();
        //    while (elementIter.MoveNext())
        //    {
        //        var element = elementIter.Current as CodeElement;
        //        var eleName = element.Name;
        //        var start = element.StartPoint;
        //        var end = element.EndPoint;
        //        var startLine = start.Line;
        //        var endLine = end.Line;
        //        string indentStr = "";
        //        for (int i = 0; i < indent; i++)
        //        {
        //            indentStr += "\t";
        //        }
        //        Logger.WriteLine(indentStr + string.Format("element:{0} {1} {2}", eleName, startLine, endLine));

        //        var children = element.Children;
        //        if (children != null)
        //        {
        //            TraverseCodeElement(children, indent+1);
        //        }
        //    }
        //}
    }
}
