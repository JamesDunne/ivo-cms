using System;
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

namespace TestCMS
{
    public abstract class CommonTestBase
    {
        protected abstract ContentEngine getContentEngine(DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null);

        protected ITreeRepository trrepo;
        protected IStreamedBlobRepository blrepo;
        protected ITreePathStreamedBlobRepository tpsbrepo;

        protected void output(HTMLFragment fragment)
        {
            Console.WriteLine(((string)fragment).Replace("\n", Environment.NewLine));
        }

        protected void output(TreePathStreamedBlob item)
        {
            TaskEx.RunEx(async () =>
            {
                output((HTMLFragment)"-----------------------------------------");
                output((HTMLFragment)(item.TreeBlobPath.Path.ToString() + ":"));
                output((HTMLFragment)Encoding.UTF8.GetString(await item.StreamedBlob.ReadStream(sr =>
                {
                    byte[] tmp = new byte[sr.Length];
                    sr.Read(tmp, 0, (int)sr.Length);
                    return tmp;
                })));
                output((HTMLFragment)"-----------------------------------------");
            }).Wait();
        }

        protected void assertTranslated(string blob, string expected)
        {
            var ce = getContentEngine();
            assertTranslated(ce, blob, expected);
        }

        protected void assertTranslated(ContentEngine ce, string blob, string expected)
        {
            var bl = new MemoryStreamedBlob(blob);
            assertTranslated(ce, bl, new TreeID(), expected);
        }

        protected void assertTranslated(ContentEngine ce, IStreamedBlob bl, TreeID rootid, string expected)
        {
            var item = new TreePathStreamedBlob(rootid, (CanonicalBlobPath)"/test", bl);
            assertTranslated(ce, item, expected);
        }

        protected void assertTranslated(ContentEngine ce, TreePathStreamedBlob item, string expected)
        {
            output(item);

            var frag = ce.RenderBlob(item);
            output(frag);

            foreach (var err in ce.GetErrors())
            {
                Console.Error.WriteLine("{0} ({1}:{2}): {3}", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
            }

            Assert.AreEqual(expected, (string)frag);
        }

        protected void assumeFail(string blob, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var ce = getContentEngine();
            assumeFail(ce, blob, expectedErrors, expectedWarnings);
        }

        protected void assumeFail(ContentEngine ce, string blob, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var bl = new MemoryStreamedBlob(blob);
            assumeFail(ce, bl, new TreeID(), expectedErrors, expectedWarnings);
        }

        protected void assumeFail(ContentEngine ce, IStreamedBlob bl, TreeID rootid, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var item = new TreePathStreamedBlob(rootid, (CanonicalBlobPath)"/test", bl);
            assumeFail(ce, item, expectedErrors, expectedWarnings);
        }

        protected void assumeFail(ContentEngine ce, TreePathStreamedBlob item, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            output(item);

            var frag = ce.RenderBlob(item);
            output(frag);

            var errors = ce.GetErrors();
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Error(s):");
                foreach (var err in errors)
                {
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
                }
            }

            var warns = ce.GetWarnings();
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
