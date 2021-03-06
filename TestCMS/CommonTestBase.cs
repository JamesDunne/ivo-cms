﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.CMS;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Repositories;
using System.Threading.Tasks;
using IVO.Definition.Models;
using IVO.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IVO.Definition.Errors;

namespace TestCMS
{
    public sealed class TestContext
    {
        public ITreeRepository trrepo { get; private set; }
        public IStreamedBlobRepository blrepo { get; private set; }
        public ITreePathStreamedBlobRepository tpsbrepo { get; private set; }
        public ContentEngine ce { get; private set; }

        public TestContext(ContentEngine ce, ITreeRepository trrepo, IStreamedBlobRepository blrepo, ITreePathStreamedBlobRepository tpsbrepo)
        {
            this.ce = ce;
            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.tpsbrepo = tpsbrepo;
        }
    }

    public abstract class CommonTestBase
    {
        public delegate TestContext GetTestContextDelegate(DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null);
        
        protected GetTestContextDelegate getTestContext;

        protected void output(HtmlFragment fragment)
        {
            Console.WriteLine(((string)fragment).Replace("\n", Environment.NewLine));
        }

        protected void output(TreePathStreamedBlob item)
        {
            output((HtmlFragment)(item.TreeBlobPath.Path.ToString() + ":"));
            output((HtmlFragment)Encoding.UTF8.GetString(
                item.StreamedBlob.ReadStream( (Func<System.IO.Stream, Errorable<byte[]>>) (sr =>
                {
                    byte[] tmp = new byte[sr.Length];
                    sr.Read(tmp, 0, (int)sr.Length);
                    return tmp;
                })).Value
            ));
            output((HtmlFragment)"-----------------------------------------");
        }

        protected void assertTranslated(string blob, string expected, params SemanticWarning[] expectedWarnings)
        {
            var tc = getTestContext();
            assertTranslated(tc, blob, expected, expectedWarnings);
        }

        protected void assertTranslated(TestContext tc, string blob, string expected, params SemanticWarning[] expectedWarnings)
        {
            var bl = new MemoryStreamedBlob(blob);
            assertTranslated(tc, bl, new TreeID(), expected, expectedWarnings);
        }

        protected void assertTranslated(TestContext tc, IStreamedBlob bl, TreeID rootid, string expected, params SemanticWarning[] expectedWarnings)
        {
            var item = new TreePathStreamedBlob(rootid, (CanonicalBlobPath)"/test", bl);
            assertTranslated(tc, item, expected, expectedWarnings);
        }

        protected void assertTranslated(TestContext tc, TreePathStreamedBlob item, string expected, params SemanticWarning[] expectedWarnings)
        {
            output(item);

            var fragTask = tc.ce.RenderBlob(item);
            fragTask.Wait();
            
            var efrag = fragTask.Result;
            Assert.IsFalse(efrag.HasErrors);

            var frag = efrag.Value;
            output(frag);

            var errors = tc.ce.GetErrors();
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Error(s):");
                foreach (var err in errors)
                {
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
                }
            }

            var warns = tc.ce.GetWarnings();
            if (warns.Count > 0)
            {
                Console.Error.WriteLine("Warning(s):");
                foreach (var warn in warns)
                {
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", warn.Item.TreeBlobPath.Path, warn.LineNumber, warn.LinePosition, warn.Message);
                }
            }

            Assert.AreEqual(expected, (string)frag);

            Assert.AreEqual(expectedWarnings.Length, warns.Count);
            CollectionAssert.AreEqual(expectedWarnings, warns, new SemanticWarningMessageComparer());
        }

        protected void assumeFail(string blob, IVO.CMS.SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var tc = getTestContext();
            assumeFail(tc, blob, expectedErrors, expectedWarnings);
        }

        protected void assumeFail(TestContext tc, string blob, IVO.CMS.SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var bl = new MemoryStreamedBlob(blob);
            assumeFail(tc, bl, new TreeID(), expectedErrors, expectedWarnings);
        }

        protected void assumeFail(TestContext tc, IStreamedBlob bl, TreeID rootid, IVO.CMS.SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var item = new TreePathStreamedBlob(rootid, (CanonicalBlobPath)"/test", bl);
            assumeFail(tc, item, expectedErrors, expectedWarnings);
        }

        protected void assumeFail(TestContext tc, TreePathStreamedBlob item, IVO.CMS.SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            output(item);

            var fragTask = tc.ce.RenderBlob(item);
            fragTask.Wait();

            var efrag = fragTask.Result;
            Assert.IsFalse(efrag.HasErrors);

            var frag = efrag.Value;
            output(frag);

            var errors = tc.ce.GetErrors();
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Error(s):");
                foreach (var err in errors)
                {
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
                }
            }

            var warns = tc.ce.GetWarnings();
            if (warns.Count > 0)
            {
                Console.Error.WriteLine("Warning(s):");
                foreach (var warn in warns)
                {
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", warn.Item.TreeBlobPath.Path, warn.LineNumber, warn.LinePosition, warn.Message);
                }
            }

            Assert.AreEqual(expectedErrors.Length, errors.Count);
            CollectionAssert.AreEqual(expectedErrors, errors, new SemanticErrorMessageComparer());
            Assert.AreEqual(expectedWarnings.Length, warns.Count);
            CollectionAssert.AreEqual(expectedWarnings, warns, new SemanticWarningMessageComparer());
        }
    }
}
