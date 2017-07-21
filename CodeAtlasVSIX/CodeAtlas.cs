//------------------------------------------------------------------------------
// <copyright file="CodeAtlas.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace CodeAtlasVSIX
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;
    using System.Windows.Controls;

    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("cccc16a7-57da-4a9a-8202-93176bb96656")]
    public class CodeAtlas : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeAtlas"/> class.
        /// </summary>
        public CodeAtlas() : base(null)
        {
            this.Caption = "Code Atlas";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            //this.Content = new CodeView();
            this.Content = UIManager.Instance().GetMainUI();
        }

        protected override void OnClose()
        {
            UIManager.Instance().GetScene().SaveConfig();
            base.OnClose();
        }
    }
}
