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
    public class SolutionTraverser
    {
        public void Traverse()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }

            try
            {
                Solution solution = dte.Solution;
                TraverseSolution(solution);
            }
            catch
            {
                Console.WriteLine("wrong project");
            }
        }

        void TraverseSolution(Solution solution)
        {
            if (solution == null)
            {
                return;
            }

            DoTraverseSolution(solution);
            string solutionFile = solution.FileName;
            Projects projectList = solution.Projects;
            int projectCount = projectList.Count;
            foreach (var proj in projectList)
            {
                var project = proj as Project;
                if (project != null)
                {
                    TraverseProject(project);
                }
            }
        }

        void TraverseProject(Project project)
        {
            if (project == null)
            {
                return;
            }

            DoTraverseProject(project);

            ProjectItems projectItems = project.ProjectItems;
            //var codeModel = project.CodeModel;
            //var codeLanguage = codeModel.Language;

            var items = projectItems.GetEnumerator();
            while (items.MoveNext())
            {
                var item = items.Current as ProjectItem;
                TraverseProjectItem(item);
            }
        }

        void TraverseProjectItem(ProjectItem item)
        {
            if (item == null)
            {
                return;
            }

            if (item.SubProject != null)
            {
                TraverseProject(item.SubProject);
            }
            var projectItems = item.ProjectItems;
            if (projectItems != null)
            {
                var items = projectItems.GetEnumerator();
                while (items.MoveNext())
                {
                    var currentItem = items.Current as ProjectItem;
                    TraverseProjectItem(currentItem);
                }
            }

            DoTraverseProjectItem(item);
        }

        protected virtual void DoTraverseSolution(Solution solution) { }
        protected virtual void DoTraverseProject(Project project) { }
        protected virtual void DoTraverseProjectItem(ProjectItem item) { }
    }

    public class ProjectFileCollector : SolutionTraverser
    {
        class PathNode
        {
            public PathNode(string name) { m_name = name; }
            public string m_name;
            public Dictionary<string, PathNode> m_children = new Dictionary<string, PathNode>();
        }

        string m_solutionName = "";
        string m_solutionPath = "";
        List<string> m_fileList = new List<string>();
        HashSet<string> m_directoryList = new HashSet<string>();
        PathNode m_rootNode = new PathNode("root");
        List<string> m_extensionList = new List<string> {
            ".c", ".cc", ".cxx", ".cpp", ".c++", ".inl",".h", ".hh", ".hxx", ".hpp", ".h++",".inc", 
            ".java", ".ii", ".ixx", ".ipp", ".i++", ".idl", ".ddl", ".odl",
            ".cs",
            ".d", ".php", ".php4", ".php5", ".phtml", ".m", ".markdown", ".md", ".mm", ".dox",
            ".py",
            ".f90", ".f", ".for",
            ".tcl",
            ".vhd", ".vhdl", ".ucf", ".qsf",
            ".as", ".js" };

        public ProjectFileCollector()
        {
        }

        public List<string> GetDirectoryList()
        {
            return m_directoryList.ToList();
        }

        public string GetSolutionPath()
        {
            return m_solutionPath;
        }

        public string GetSolutionFolder()
        {
            if (m_solutionPath == "")
            {
                return "";
            }
            return System.IO.Path.GetDirectoryName(m_solutionPath).Replace('\\','/');
        }

        public string GetSolutionName()
        {
            return m_solutionName;
        }

        protected override void DoTraverseSolution(Solution solution)
        {
            m_solutionPath = solution.FileName;
            if (m_solutionPath != "")
            {
                m_solutionName = System.IO.Path.GetFileNameWithoutExtension(m_solutionPath);
            }
        }
        protected override void DoTraverseProject(Project project)
        {
            Logger.WriteLine("projectname: " + project.Name);
        }
        protected override void DoTraverseProjectItem(ProjectItem item)
        {
            string itemName = item.Name;
            string itemKind = item.Kind;
            if (itemKind == Constants.vsProjectItemKindPhysicalFolder)
            {

            }
            else if (itemKind == Constants.vsProjectItemKindPhysicalFile)
            {
                for (short i = 0; i < item.FileCount; i++)
                {
                    string fileName = item.FileNames[i];
                    m_fileList.Add(fileName);
                    Logger.WriteLine(fileName);

                    //var pathComponents = fileName.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    //PathNode node = m_rootNode;
                    //foreach (var pathComp in pathComponents)
                    //{
                    //    if (!node.m_children.ContainsKey(pathComp))
                    //    {
                    //        node.m_children[pathComp] = new PathNode(pathComp);
                    //    }
                    //    node = node.m_children[pathComp];
                    //}

                    var ext = System.IO.Path.GetExtension(fileName).ToLower();
                    foreach (var extension in m_extensionList)
                    {
                        if (ext == extension)
                        {
                            var directory = System.IO.Path.GetDirectoryName(fileName);
                            directory = directory.Replace('\\', '/');
                            m_directoryList.Add(directory);
                            break;
                        }
                    }
                }
            }
        }
    }
}
