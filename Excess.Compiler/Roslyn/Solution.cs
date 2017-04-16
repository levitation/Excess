﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Excess.Compiler.Roslyn
{
    using ExtensionFunction = Action<RoslynCompiler, Scope>;

    class ExcessDocument
    {
        public DocumentId Id { get; set; }
        public DocumentId CSharpDocument { get; set; }
        public RoslynDocument Document { get; set; }
        public RoslynCompiler Compiler { get; set; }
        public Dictionary<int, int> Mapping { get; set; }
    }

    public class ExcessSolution
    {
        public static ExcessSolution FromFile(string solutionFile)
        {
            throw new NotImplementedException();
        }

        public static ExcessSolution FromFiles(Dictionary<string, string> fileContents)
        {
            throw new NotImplementedException();
        }

        ExcessSolution _parent;
        Solution _solution;
        IDictionary<string, ExtensionFunction> _extensions;
        public ExcessSolution(ExcessSolution parent, Solution solution, IDictionary<string, ExtensionFunction> extensions)
        {
            _parent = parent;
            _solution = solution;
            _extensions = extensions;
        }

        Dictionary<DocumentId, ExcessDocument> _documents;
        public ExcessSolution ApplyChanges(DocumentId document)
        {
            if (_documents == null)
            {
                if (_parent == null)
                {
                    build();
                    return this;
                }
            }

            return new ExcessSolution(this, _solution, _extensions);
        }

        private void build()
        {
            var solution = _solution;

            Debug.Assert(_documents == null);
            _documents = new Dictionary<DocumentId, ExcessDocument>();
            foreach (var project in _solution.Projects)
            {
                foreach (var doc in project.AdditionalDocuments)
                {
                    if (Path.GetExtension(doc.Name) == ".xs")
                        _documents[doc.Id] = loadDocument(solution, doc, out solution);
                }
            }

            applySemantical();
        }

        private void applySemantical()
        {
            bool building = true;
            int tries = 5;
            while (building && tries > 0)
            {
                foreach (var kvp in _documents)
                {
                    var document = kvp.Value.Document;
                    var compiler = kvp.Value.Compiler;
                    if (document.Stage == CompilerStage.Started)
                        compiler.Compile(document.Text, CompilerStage.Syntactical);

                    if (document.Stage < CompilerStage.Semantical)
                        building &= compiler.Advance(CompilerStage.Semantical);
                }

                tries--;
            }
        }

        private ExtensionFunction getExtension(string name)
        {
            var extension = null as ExtensionFunction;
            _extensions.TryGetValue(name, out extension);
            return extension;
        }

        private ExcessDocument loadDocument(Solution solution, TextDocument doc, out Solution result)
        {
            SourceText text;
            if (!doc.TryGetText(out text))
                throw new InvalidOperationException($"Cannot read from {doc.Name}");

            var contents = text.ToString();
            var document = new RoslynDocument(new Scope(null), contents); //port: rid of scope

            var compilerResult = new RoslynCompiler(null); //port: rid of scope
            var tree = CSharpSyntaxTree.ParseText(contents);
            var usings = (tree.GetRoot() as CompilationUnitSyntax)
                ?.Usings
                .Where(@using =>
                {
                    var usingId = @using.Name.ToString();
                    if (!usingId.StartsWith("xs."))
                        return false;

                    usingId = usingId.Substring("xs.".Length);

                    var extension = getExtension(usingId);
                    if (extension != null)
                    {
                        extension(compilerResult, null); //td: props?
                        return true;
                    }

                    return false;
                }).ToArray();

            result = solution; //td: add needed cs files, etc
            var filename = doc.Name + ".cs";
            var fileid = doc
                .Project
                .Documents
                .FirstOrDefault(d => d.Name == filename)
                ?.Id;

            if (fileid == null)
            {
                var newFile = doc.Project.AddDocument(filename, "");
                result = newFile.Project.Solution;
            }

            return new ExcessDocument
            {
                Id = doc.Id,
                CSharpDocument = fileid,
                Compiler = compilerResult,
                Document = document,
            };
        }
    }
}
