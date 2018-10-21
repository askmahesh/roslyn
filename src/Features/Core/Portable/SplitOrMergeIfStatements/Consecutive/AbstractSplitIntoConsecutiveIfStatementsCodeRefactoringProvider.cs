﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.SplitOrMergeIfStatements
{
    internal abstract class AbstractSplitIntoConsecutiveIfStatementsCodeRefactoringProvider<TExpressionSyntax>
        : AbstractSplitIfStatementCodeRefactoringProvider
        where TExpressionSyntax : SyntaxNode
    {
        protected abstract bool HasElseClauses(SyntaxNode ifStatement);

        protected abstract (SyntaxNode, SyntaxNode) SplitIfStatementIntoElseClause(
            SyntaxNode currentIfStatement, TExpressionSyntax condition1, TExpressionSyntax condition2);

        protected abstract (SyntaxNode, SyntaxNode) SplitIfStatementIntoSeparateStatements(
            SyntaxNode currentIfStatement, TExpressionSyntax condition1, TExpressionSyntax condition2);

        protected sealed override CodeAction CreateCodeAction(Document document, TextSpan span)
            => new MyCodeAction(c => FixAsync(document, span, c), IfKeywordText);

        private async Task<Document> FixAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = root.FindToken(span.Start);

            Contract.ThrowIfFalse(IsPartOfBinaryExpressionChain(token, LogicalExpressionSyntaxKind, out var rootExpression));
            Contract.ThrowIfFalse(IsConditionOfIfStatement(rootExpression, out var currentIfStatement));

            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();

            var (left, right) = SplitBinaryExpressionChain(token, rootExpression, syntaxFacts);

            var (firstIfStatement, secondIfStatement) = await CanBeSeparateStatementsAsync(document, syntaxFacts, currentIfStatement, cancellationToken)
                ? SplitIfStatementIntoSeparateStatements(currentIfStatement, (TExpressionSyntax)left, (TExpressionSyntax)right)
                : SplitIfStatementIntoElseClause(currentIfStatement, (TExpressionSyntax)left, (TExpressionSyntax)right);

            var newNodes = secondIfStatement != null
                ? ImmutableArray.Create(
                    firstIfStatement.WithAdditionalAnnotations(Formatter.Annotation),
                    secondIfStatement.WithAdditionalAnnotations(Formatter.Annotation))
                : ImmutableArray.Create(
                    firstIfStatement.WithAdditionalAnnotations(Formatter.Annotation));

            var newRoot = root.ReplaceNode(currentIfStatement, newNodes);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<bool> CanBeSeparateStatementsAsync(
            Document document,
            ISyntaxFactsService syntaxFacts,
            SyntaxNode ifStatement,
            CancellationToken cancellationToken)
        {
            if (HasElseClauses(ifStatement))
            {
                return false;
            }

            if (!syntaxFacts.IsExecutableBlock(ifStatement.Parent))
            {
                return false;
            }

            var insideStatements = syntaxFacts.GetStatementContainerStatements(ifStatement);
            if (insideStatements.Count == 0)
            {
                return false;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var controlFlow = semanticModel.AnalyzeControlFlow(insideStatements.First(), insideStatements.Last());

            return !controlFlow.EndPointIsReachable;
        }

        private sealed class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument, string ifKeywordText)
                : base(string.Format(FeaturesResources.Split_into_consecutive_0_statements, ifKeywordText), createChangedDocument)
            {
            }
        }
    }
}
