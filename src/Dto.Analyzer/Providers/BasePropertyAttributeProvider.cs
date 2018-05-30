﻿using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dto.Analyzer.Providers
{
  public abstract class BasePropertyAttributeProvider : CodeRefactoringProvider
  {
    private string AddAttributeActionTitle => $"Add {AttributeName} attribute";
    private string RemoveAttributeActionTitle => $"Remove {AttributeName} attribute";

    protected abstract string AttributeName { get; }

    protected abstract PropertyDeclarationSyntax AddAttribute(PropertyDeclarationSyntax prop);

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
      // Some general variables we need for further processing and some basic checks to exit as early as possible.
      var document = context.Document;
      if (document.Project.Solution.Workspace.Kind == WorkspaceKind.MiscellaneousFiles)
        return;
      var span = context.Span;
      if (!span.IsEmpty)
        return;
      var cancellationToken = context.CancellationToken;
      if (cancellationToken.IsCancellationRequested)
        return;

      var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
      var root = await model.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

      var token = root.FindToken(span.Start);
      if (!token.IsKind(SyntaxKind.IdentifierToken))
      {
        // Not the name identifier -> don't offer a refactoring here
        return;
      }

      if (token.Parent is ClassDeclarationSyntax classDeclaration)
      {
        if (classDeclaration.IsDto() &&
            classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>().Any(p => !p.ContainsAttribute(AttributeName)))
        {
          context.RegisterRefactoring(CodeAction.Create(AddAttributeActionTitle,
            t => AddAttributesClassAction(t, context.Document, root, classDeclaration)));
        }
        if (classDeclaration.IsDto() &&
            classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>().Any(p => p.ContainsAttribute(AttributeName)))
        {
          context.RegisterRefactoring(CodeAction.Create(RemoveAttributeActionTitle,
            t => RemoveAttributesClassAction(t, context.Document, root, classDeclaration)));
        }
      }
      else if (token.Parent is PropertyDeclarationSyntax propertyDeclaration)
      {
        if (propertyDeclaration.Parent is ClassDeclarationSyntax parentClass)
        {
          if (parentClass.IsDto())
          {
            if (propertyDeclaration.ContainsAttribute(AttributeName))
            {
              context.RegisterRefactoring(CodeAction.Create(RemoveAttributeActionTitle,
                t => RemoveAttributesPropertyAction(t, context.Document, root, propertyDeclaration)));
            }
            else
            {
              context.RegisterRefactoring(CodeAction.Create(AddAttributeActionTitle,
                t => AddAttributesPropertyAction(t, context.Document, root, propertyDeclaration)));
            }
          }
        }
      }
    }

    protected virtual Task<Document> AddAttributesClassAction(CancellationToken token, Document document, SyntaxNode root, ClassDeclarationSyntax classDeclaration)
    {
      if (token.IsCancellationRequested)
        return Task.FromResult(document);

      var newRoot = root.ReplaceNodes(
        classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>().Where(p => !p.ContainsAttribute(AttributeName)),
        (originalProp, newProp) => AddAttribute(originalProp));

      return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    protected virtual Task<Document> RemoveAttributesClassAction(CancellationToken token, Document document, SyntaxNode root, ClassDeclarationSyntax classDeclaration)
    {
      if (token.IsCancellationRequested)
        return Task.FromResult(document);

      var newRoot = root.ReplaceNodes(
        classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>(),
        (originalProp, newProp) => originalProp.RemoveAttribute(AttributeName));

      return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    protected virtual Task<Document> AddAttributesPropertyAction(CancellationToken token, Document document, SyntaxNode root, PropertyDeclarationSyntax propertyDeclaration)
    {
      if (token.IsCancellationRequested)
        return Task.FromResult(document);

      var newRoot = root.ReplaceNode(propertyDeclaration, AddAttribute(propertyDeclaration));
      return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    protected virtual Task<Document> RemoveAttributesPropertyAction(CancellationToken token, Document document, SyntaxNode root, PropertyDeclarationSyntax propertyDeclaration)
    {
      if (token.IsCancellationRequested)
        return Task.FromResult(document);

      var newRoot = root.ReplaceNode(propertyDeclaration, propertyDeclaration.RemoveAttribute(AttributeName));
      return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
  }
}
