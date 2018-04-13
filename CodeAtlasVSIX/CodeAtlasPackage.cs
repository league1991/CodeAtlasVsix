//------------------------------------------------------------------------------
// <copyright file="CodeAtlasPackage.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.2.1", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(CodeAtlas))]
    [Guid(CodeAtlasPackage.PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class CodeAtlasPackage : Package
    {
        /// <summary>
        /// CodeAtlasPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "ad6e432c-b58e-4dc8-9893-6e1b412d38c4";

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeAtlas"/> class.
        /// </summary>
        public CodeAtlasPackage()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            CodeAtlasCommand.Initialize(this);
            base.Initialize();
            var mainUI = UIManager.Instance().GetMainUI();
            mainUI.SetPackage(this);
            AddCommand(0x0103, mainUI.OnFindCallers);
            AddCommand(0x0104, mainUI.OnFindCallees);
            AddCommand(0x0105, mainUI.OnFindMembers);
            AddCommand(0x0106, mainUI.OnFindOverrides);
            AddCommand(0x0107, mainUI.OnFindBases);
            AddCommand(0x0108, mainUI.OnFindUses);
            AddCommand(0x0109, mainUI.OnGoUp);
            AddCommand(0x010a, mainUI.OnGoDown);
            AddCommand(0x010b, mainUI.OnGoLeft);
            AddCommand(0x010c, mainUI.OnGoRight);
            AddCommand(0x0130, mainUI.OnGoUpInOrder);
            AddCommand(0x0131, mainUI.OnGoDownInOrder);
            AddCommand(0x010d, mainUI.OnDelectSelectedItems);
            AddCommand(0x011b, mainUI.OnDeleteNearbyItems);
            AddCommand(0x010e, mainUI.OnDeleteSelectedItemsAndAddToStop);
            AddCommand(0x010f, mainUI.OnAddSimilarCodeItem);
            AddCommand(0x0110, mainUI.OnShowInAtlas);
            AddCommand(0x011c, mainUI.OnShowDefinitionInAtlas);

            AddCommand(0x0111, mainUI.OnShowScheme1);
            AddCommand(0x0112, mainUI.OnShowScheme2);
            AddCommand(0x0113, mainUI.OnShowScheme3);
            AddCommand(0x0114, mainUI.OnShowScheme4);
            AddCommand(0x0115, mainUI.OnShowScheme5);
            AddCommand(0x0116, mainUI.OnToggleScheme1);
            AddCommand(0x0117, mainUI.OnToggleScheme2);
            AddCommand(0x0118, mainUI.OnToggleScheme3);
            AddCommand(0x0119, mainUI.OnToggleScheme4);
            AddCommand(0x011a, mainUI.OnToggleScheme5);

            AddCommand(0x011d, mainUI.OnFastAnalyseSolutionButton);
            AddCommand(0x011e, mainUI.OnFastAnalyseProjectsButton);
            AddCommand(0x0124, mainUI.OnOpen);
            AddCommand(0x0125, mainUI.OnClose);

            AddCommand(0x0128, mainUI.OnBeginCustomEdge);
            AddCommand(0x0129, mainUI.OnEndCustomEdge);

            AddCommand(0x0133, mainUI.ToggleAnchor);

            AddCommand(0x0135, mainUI.ShowProject);
            AddCommand(0x0136, mainUI.ShowAllProjects);
            UIManager.Instance().GetScene().StartThread();
        }

        void AddCommand(int commandID, ExecutedRoutedEventHandler handler)
        {
            CodeAtlasVSIX.Commands.VSMenuCommand.Initialize(this, commandID, handler);
        }

        #endregion
    }
}
