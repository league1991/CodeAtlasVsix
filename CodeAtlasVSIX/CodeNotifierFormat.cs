//------------------------------------------------------------------------------
// <copyright file="CodeNotifierFormat.cs" company="me">
//     Copyright (c) me.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Defines an editor format for the CodeNotifier type that has a purple background
    /// and is underlined.
    /// </summary>
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "CodeNotifier")]
    [Name("CodeNotifier")]
    [UserVisible(true)] // This should be visible to the end user
    [Order(Before = Priority.Default)] // Set the priority to be after the default classifiers
    internal sealed class CodeNotifierFormat : ClassificationFormatDefinition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeNotifierFormat"/> class.
        /// </summary>
        public CodeNotifierFormat()
        {
            this.DisplayName = "CodeNotifier"; // Human readable version of the name
            this.BackgroundColor = Colors.BlueViolet;
            this.TextDecorations = System.Windows.TextDecorations.Underline;
        }
    }
}
