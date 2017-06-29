//------------------------------------------------------------------------------
// <copyright file="CodeNotifierClassificationDefinition.cs" company="me">
//     Copyright (c) me.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace CodeAtlasVSIX
{
    /// <summary>
    /// Classification type definition export for CodeNotifier
    /// </summary>
    internal static class CodeNotifierClassificationDefinition
    {
        // This disables "The field is never used" compiler's warning. Justification: the field is used by MEF.
#pragma warning disable 169

        /// <summary>
        /// Defines the "CodeNotifier" classification type.
        /// </summary>
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("CodeNotifier")]
        private static ClassificationTypeDefinition typeDefinition;

#pragma warning restore 169
    }
}
