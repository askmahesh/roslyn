﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.VisualStudio.Text;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Suggestions
{
    internal partial class SuggestedActionWithNestedFlavors
    {
        /// <summary>
        /// Suggested action for showing the preview-changes dialog.  Note: this is only used
        /// as a 'flavor' inside CodeFixSuggestionAction and CodeRefactoringSuggestedAction.
        /// </summary>
        private sealed partial class PreviewChangesSuggestedAction : SuggestedAction
        {
            private PreviewChangesSuggestedAction(
                IThreadingContext threadingContext,
                SuggestedActionsSourceProvider sourceProvider,
                Workspace workspace,
                ITextBuffer subjectBuffer,
                object provider,
                PreviewChangesCodeAction codeAction)
                : base(threadingContext, sourceProvider, workspace, subjectBuffer, provider, codeAction)
            {
            }

            public static async Task<SuggestedAction> CreateAsync(
                SuggestedActionWithNestedFlavors suggestedAction, CancellationToken cancellationToken)
            {
                var previewResult = await suggestedAction.GetPreviewResultAsync(cancellationToken).ConfigureAwait(true);
                if (previewResult == null)
                {
                    return null;
                }

                var changeSummary = previewResult.ChangeSummary;
                if (changeSummary == null)
                {
                    return null;
                }

                return new PreviewChangesSuggestedAction(
                    suggestedAction.ThreadingContext,
                    suggestedAction.SourceProvider, suggestedAction.Workspace,
                    suggestedAction.SubjectBuffer, suggestedAction.Provider,
                    new PreviewChangesCodeAction(
                        suggestedAction.Workspace, suggestedAction.CodeAction, changeSummary));
            }
        }
    }
}
