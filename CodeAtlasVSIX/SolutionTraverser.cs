using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeAtlasVSIX
{
    public class ProjectData
    {
        public string m_uniqueName = "";
        public string m_name = "";
        public string m_path = "";
        public ProjectData(string uname, string name, string path)
        {
            m_uniqueName = uname;
            m_name = name;
            m_path = path;
        }

        static public List<ProjectData> GetSelectedProject()
        {
            List<ProjectData> result = new List<ProjectData>();
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            var activeProjects = dte.ActiveSolutionProjects as Array;
            if (activeProjects != null)
            {
                foreach (var item in activeProjects)
                {
                    var project = item as Project;
                    if (project != null)
                    {
                        result.Add(new ProjectData(project.UniqueName, project.Name, project.FileName));
                    }
                }
            }
            return result;
        }

    }

    public class SolutionTraverser
    {
        public void Traverse()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }
            
            Solution solution = dte.Solution;
            TraverseSolution(solution);
        }

        void TraverseSolution(Solution solution)
        {
            if (solution == null)
            {
                return;
            }

            if(BeforeTraverseSolution(solution))
            {
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
                AfterTraverseSolution(solution);
            }
        }

        void TraverseProject(Project project)
        {
            if (project == null)
            {
                return;
            }

            if(BeforeTraverseProject(project))
            {
                ProjectItems projectItems = project.ProjectItems;
                if (projectItems != null)
                {
                    //var codeModel = project.CodeModel;
                    //var codeLanguage = codeModel.Language;

                    var items = projectItems.GetEnumerator();
                    while (items.MoveNext())
                    {
                        var item = items.Current as ProjectItem;
                        TraverseProjectItem(item);
                    }
                }
                AfterTraverseProject(project);
            }

        }

        void TraverseProjectItem(ProjectItem item)
        {
            if (item == null)
            {
                return;
            }

            if (BeforeTraverseProjectItem(item))
            {
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
                AfterTraverseProjectItem(item);
            }
        }

        protected virtual bool BeforeTraverseSolution(Solution solution) { return true; }
        protected virtual bool BeforeTraverseProject(Project project) { return true; }
        protected virtual bool BeforeTraverseProjectItem(ProjectItem item) { return true; }

        protected virtual void AfterTraverseSolution(Solution solution) { }
        protected virtual void AfterTraverseProject(Project project) { }
        protected virtual void AfterTraverseProjectItem(ProjectItem item) { }
    }

    public class ProjectFileCollector : SolutionTraverser
    {
        class PathNode
        {
            public PathNode(string name) { m_name = name; }
            public string m_name;
            public Dictionary<string, PathNode> m_children = new Dictionary<string, PathNode>();
        }

        class ProjectInfo
        {
            public HashSet<string> m_includePath = new HashSet<string>();
            public HashSet<string> m_defines = new HashSet<string>();
            public string m_language = "";

            public string m_name = "";
            public string m_path = "";
            public string m_uniqueName = "";
        }

        string m_solutionName = "";
        string m_solutionPath = "";
        bool m_onlySelectedProjects = false;
        HashSet<string> m_selectedProjID = new HashSet<string>();
        SortedSet<string> m_selectedProjName = new SortedSet<string>();
        List<string> m_fileList = new List<string>();
        HashSet<string> m_directoryList = new HashSet<string>();
        PathNode m_rootNode = new PathNode("root");
        // Dictionary<string, HashSet<string>> m_projectIncludePath = new Dictionary<string, HashSet<string>>();
        Dictionary<string, ProjectInfo> m_projectInfo = new Dictionary<string, ProjectInfo>();
        HashSet<string> m_customMacroList = new HashSet<string>();
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

        public void SetCustomExtension(Dictionary<string, string> extDict)
        {
            foreach (var item in extDict)
            {
                string ext = item.Key;
                if (!ext.StartsWith("."))
                {
                    ext = "." + ext;
                }
                m_extensionList.Add(ext);
            }
        }

        public void SetCustomMacro(HashSet<string> macroSet)
        {
            if (macroSet != null)
            {
                m_customMacroList = macroSet;
            }
        }

        public void SetToSelectedProjects()
        {
            m_onlySelectedProjects = true;
            m_selectedProjID.Clear();
            m_selectedProjName.Clear();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            var activeProjects = dte.ActiveSolutionProjects as Array;
            if (activeProjects != null)
            {
                foreach (var item in activeProjects)
                {
                    var project = item as Project;
                    if (project != null)
                    {
                        m_selectedProjName.Add(project.Name);
                        SetProjectParentSelected(project);
                    }
                }
            }
        }

        public Dictionary<string, int> GetLanguageCount()
        {
            var res = new Dictionary<string, int>();
            foreach (var projectPair in m_projectInfo)
            {
                string lang = projectPair.Value.m_language;
                if (!res.ContainsKey(lang))
                {
                    res[lang] = 0;
                }
                res[lang] += 1;
            }
            return res;
        }

        public string GetMainLanguage()
        {
            string mainLanguage = "";
            int count = 0;
            var langDict = GetLanguageCount();
            foreach (var item in langDict)
            {
                if (item.Value > count)
                {
                    count = item.Value;
                    mainLanguage = item.Key;
                }
            }
            return mainLanguage;
        }

        void SetProjectParentSelected(Project project)
        {
            if (project == null)
            {
                return;
            }
            m_selectedProjID.Add(project.UniqueName);

            var parentItem = project.ParentProjectItem as ProjectItem;
            if (parentItem != null)
            {
                var containingProj = parentItem.ContainingProject;
                SetProjectParentSelected(containingProj);
            }
        }

        public List<string> GetSelectedProjectName()
        {
            return m_selectedProjName.ToList();
        }

        public List<string> GetDirectoryList()
        {
            return m_directoryList.ToList();
        }

        public List<string> GetAllIncludePath()
        {
            var res = new HashSet<string>();
            foreach (var projectPair in m_projectInfo)
            {
                var includeList = projectPair.Value.m_includePath.ToList();
                foreach (var include in includeList)
                {
                    res.Add(include);
                }
            }
            return res.ToList();
        }

        public List<string> GetAllDefines()
        {
            var res = new HashSet<string>();
            foreach (var projectPair in m_projectInfo)
            {
                var defineList = projectPair.Value.m_defines.ToList();
                foreach (var define in defineList)
                {
                    res.Add(define);
                }
            }

            res.UnionWith(m_customMacroList);
            return res.ToList();
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

        protected override bool BeforeTraverseSolution(Solution solution)
        {
            m_solutionPath = solution.FileName;
            if (m_solutionPath != "")
            {
                m_solutionName = System.IO.Path.GetFileNameWithoutExtension(m_solutionPath);
            }
            return true;
        }

        protected override bool BeforeTraverseProject(Project project)
        {
            //var propertyIter = project.Properties.GetEnumerator();
            //while (propertyIter.MoveNext() && false)
            //{
            //    var item = propertyIter.Current as Property;
            //    if (item == null)
            //    {
            //        continue;
            //    }

            //    string propName = item.Name;
            //    string propValue = "";
            //    try
            //    {
            //        propValue = item.Value.ToString();
            //    }
            //    catch
            //    {

            //    }
            //    Logger.WriteLine("   " + propName + ":" + propValue);
            //}
            try
            {
                // Skip unselected projects
                if (m_onlySelectedProjects)
                {
                    var projectID = project.UniqueName;
                    if (!m_selectedProjID.Contains(projectID))
                    {
                        return false;
                    }
                }

                Logger.Debug("Traversing Project:" + project.Name);
                var codeModel = project.CodeModel;
                if (codeModel != null)
                {
                    var projInfo = FindProjectInfo(project.UniqueName);
                    projInfo.m_name = project.Name;
                    projInfo.m_uniqueName = project.UniqueName;
                    projInfo.m_path = project.FullName;
                    if (codeModel.Language == CodeModelLanguageConstants.vsCMLanguageCSharp)
                    {
                        projInfo.m_language = "csharp";
                    }
                    else if (codeModel.Language == CodeModelLanguageConstants.vsCMLanguageVC)
                    {
                        projInfo.m_language = "cpp";
                    }
                    else
                    {
                        projInfo.m_language = "";
                    }
                }
                //var configMgr = project.ConfigurationManager;
                //var config = configMgr.ActiveConfiguration as Configuration;

                var vcProject = project.Object as VCProject;
                Logger.Debug("check vc project");
                if (vcProject != null)
                {
                    var vccon = vcProject.ActiveConfiguration as VCConfiguration;
                    IVCRulePropertyStorage generalRule = vccon.Rules.Item("ConfigurationDirectories");
                    IVCRulePropertyStorage cppRule = vccon.Rules.Item("CL");

                    // Parsing include path
                    string addIncPath = cppRule.GetEvaluatedPropertyValue("AdditionalIncludeDirectories");
                    string incPath = generalRule.GetEvaluatedPropertyValue("IncludePath");
                    string allIncPath = incPath + ";" + addIncPath;
                    string[] pathList = allIncPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var projectInc = new HashSet<string>();
                    var projectPath = Path.GetDirectoryName(project.FileName);
                    foreach (var item in pathList)
                    {
                        string path = item.Trim();
                        if (!path.Contains(":"))
                        {
                            // relative path
                            path = Path.Combine(projectPath, path);
                            path = Path.GetFullPath((new Uri(path)).LocalPath);
                        }
                        if (!Directory.Exists(path))
                        {
                            continue;
                        }
                        path = path.Replace('\\', '/').Trim();
                        projectInc.Add(path);
                        Logger.Debug("include path:" + path);
                    }
                    var projInfo = FindProjectInfo(project.UniqueName);
                    projInfo.m_includePath = projectInc;

                    // Parsing define
                    string defines = cppRule.GetEvaluatedPropertyValue("PreprocessorDefinitions");
                    string[] defineList = defines.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var defineSet = new HashSet<string>();
                    foreach (var item in defineList)
                    {
                        defineSet.Add(item);
                    }
                    projInfo.m_defines = defineSet;
                }

                //foreach (VCConfiguration vccon in vcProject.Configurations)
                //{
                //    string ruleStr = "ConfigurationDirectories";
                //    IVCRulePropertyStorage generalRule = vccon.Rules.Item(ruleStr);
                //    IVCRulePropertyStorage cppRule = vccon.Rules.Item("CL");

                //    string addIncPath = cppRule.GetEvaluatedPropertyValue("AdditionalIncludeDirectories");

                //    string incPath = generalRule.GetEvaluatedPropertyValue("IncludePath");
                //    string outputPath = vccon.OutputDirectory;

                //    //vccon.OutputDirectory = "$(test)";
                //    //string test1 = generalRule.GetEvaluatedPropertyValue(2);
                //    //string incPath = generalRule.GetEvaluatedPropertyValue("IncludePath");
                //    //string name = generalRule.GetEvaluatedPropertyValue("TargetName");
                //    Logger.WriteLine("include path:" + incPath);
                //}

                //dynamic propertySheet = vcConfig.PropertySheets;
                //IVCCollection propertySheetCollection = propertySheet as IVCCollection;
                //foreach (var item in propertySheetCollection)
                //{
                //    var vcPropertySheet = item as VCPropertySheet;
                //    if (vcPropertySheet != null)
                //    {
                //        foreach(var rule in vcPropertySheet.Rules)
                //        {
                //            var vcRule = rule as IVCRulePropertyStorage;
                //            if (vcRule != null)
                //            {
                //                vcRule.GetEvaluatedPropertyValue()
                //            }
                //        }
                //    }
                //}
                //var config = vcProject.ActiveConfiguration;
                //if (config != null)
                //{
                //    var configProps = config.Properties;
                //    var configPropIter = configProps.GetEnumerator();
                //    while (configPropIter.MoveNext())
                //    {
                //        var configProp = configPropIter.Current as Property;
                //        var configName = configProp.Name;
                //        var configVal = "";
                //        try
                //        {
                //            configVal = configProp.Value.ToString();
                //        }
                //        catch
                //        {
                //        }
                //        Logger.WriteLine("  " + configName + ":" + configVal);
                //    }

                //    //Logger.WriteLine("group----------------------------");
                //    //var groups = config.OutputGroups;
                //    //var groupIter = groups.GetEnumerator();
                //    //while (groupIter.MoveNext())
                //    //{
                //    //    var group = groupIter.Current as OutputGroup;
                //    //    group.
                //    //}
                //}
            }
            catch
            {
                Logger.Debug("project error-------------");
            }
            return true;
        }

        ProjectInfo FindProjectInfo(string name)
        {
            if (!m_projectInfo.ContainsKey(name))
            {
                m_projectInfo[name] = new ProjectInfo();
            }
            return m_projectInfo[name];
        }

        protected override bool BeforeTraverseProjectItem(ProjectItem item)
        {
            string itemName = item.Name;
            string itemKind = item.Kind.ToUpper();
            if (itemKind == Constants.vsProjectItemKindPhysicalFolder)
            {
            }
            else if (itemKind == Constants.vsProjectItemKindPhysicalFile)
            {
                for (short i = 0; i < item.FileCount; i++)
                {
                    string fileName = item.FileNames[i];
                    m_fileList.Add(fileName);
                    //Logger.WriteLine(fileName);

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
            return true;
        }
    }


    public class ProjectCounter : SolutionTraverser
    {
        class ProjectInfo
        {
        }

        Dictionary<string, ProjectInfo> m_projectInfo = new Dictionary<string, ProjectInfo>();
        int m_projectItems = 0;
        int m_projects = 0;

        public ProjectCounter()
        {
        }

        protected override bool BeforeTraverseProject(Project project)
        {
            m_projects += 1;
            return true;
        }

        protected override bool BeforeTraverseProjectItem(ProjectItem item)
        {
            m_projectItems += 1;
            return true;
        }

        public int GetTotalProjectItems()
        {
            return m_projectItems;
        }

        public int GetTotalProjects()
        {
            return m_projects;
        }


    }
}
