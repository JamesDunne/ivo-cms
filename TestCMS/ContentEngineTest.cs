using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IVO.Definition.Models;
using IVO.CMS;
using IVO.Definition.Repositories;
using IVO.Implementation.SQL;
using Asynq;
using IVO.Definition.Containers;
using System.Threading.Tasks;
using IVO.CMS.Providers;
using IVO.CMS.Providers.CustomElements;

namespace TestCMS
{
    [TestClass]
    public class ContentEngineTest
    {
        private void output(HTMLFragment fragment)
        {
            Console.WriteLine(((string)fragment).Replace("\n", Environment.NewLine));
        }

        private void output(TreePathStreamedBlob item)
        {
            output((HTMLFragment)"-----------------------------------------");
            output((HTMLFragment)(item.Path.ToString() + ":"));
            output((HTMLFragment)Encoding.UTF8.GetString(item.Blob.Contents));
            output((HTMLFragment)"-----------------------------------------");
        }

        private DataContext getDataContext()
        {
            return new DataContext(@"Data Source=.\SQLEXPRESS;Initial Catalog=IVO;Integrated Security=SSPI");
        }

        private DataContext db;
        private ITreeRepository trrepo;
        private IBlobRepository blrepo;

        private ContentEngine getContentEngine(DateTimeOffset? viewDate = null, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null)
        {
            DateTimeOffset realDate = viewDate ?? DateTimeOffset.Now;

            db = getDataContext();
            trrepo = new TreeRepository(db);
            blrepo = new BlobRepository(db);
            return new ContentEngine(trrepo, blrepo, realDate, evaluator, provider);
        }

        private void assertTranslated(string blob, string expected)
        {
            var ce = getContentEngine();
            assertTranslated(ce, blob, expected);
        }

        private void assertTranslated(ContentEngine ce, string blob, string expected)
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(blob));
            assertTranslated(ce, bl, new TreeID(), expected);
        }

        private void assertTranslated(ContentEngine ce, Blob bl, TreeID rootid, string expected)
        {
            var item = new TreePathStreamedBlob(rootid, (CanonicalBlobPath)"/test", bl);
            assertTranslated(ce, item, expected);
        }

        private void assertTranslated(ContentEngine ce, TreePathStreamedBlob item, string expected)
        {
            output(item);

            var frag = ce.RenderBlob(item);
            output(frag);

            foreach (var err in ce.GetErrors())
            {
                Console.Error.WriteLine("{0} ({1}:{2}): {3}", err.Item.Path, err.LineNumber, err.LinePosition, err.Message);
            }

            Assert.AreEqual(expected, (string)frag);
        }

        private void assumeFail(string blob, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var ce = getContentEngine();
            assumeFail(ce, blob, expectedErrors, expectedWarnings);
        }

        private void assumeFail(ContentEngine ce, string blob, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(blob));
            assumeFail(ce, bl, new TreeID(), expectedErrors, expectedWarnings);
        }

        private void assumeFail(ContentEngine ce, Blob bl, TreeID rootid, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var item = new TreePathStreamedBlob(rootid, (CanonicalBlobPath)"/test", bl);
            assumeFail(ce, item, expectedErrors, expectedWarnings);
        }

        private void assumeFail(ContentEngine ce, TreePathStreamedBlob item, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
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
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", err.Item.Path, err.LineNumber, err.LinePosition, err.Message);
                }
            }

            var warns = ce.GetWarnings();
            if (warns.Count > 0)
            {
                Console.Error.WriteLine("Warning(s):");
                foreach (var warn in warns)
                {
                    Console.Error.WriteLine("  {0} ({1}:{2}): {3}", warn.Item.Path, warn.LineNumber, warn.LinePosition, warn.Message);
                }
            }

            Assert.AreEqual(expectedErrors.Length, errors.Count);
            CollectionAssert.AreEqual(expectedErrors, errors, new SemanticErrorMessageComparer());
            Assert.AreEqual(expectedWarnings.Length, warns.Count);
            CollectionAssert.AreEqual(expectedWarnings, warns, new SemanticWarningMessageComparer());
        }

        [TestMethod]
        public void TestRenderBlob()
        {
            assertTranslated(
                "<a><b/><c/></a>\r\n<b></b>",
                "<a><b /><c /></a>\r\n<b></b>"
            );
        }

        [TestMethod]
        public void TestRenderBlobAttributes()
        {
            assertTranslated(
                "<a style=\"color: &amp;too&quot;here&quot;\" href=\"http://www.google.com/?a=1&amp;b=2\" target=\"_blank\"><b /><c /></a>\r\n<b class=\"abc\"></b>",
                "<a style=\"color: &amp;too&quot;here&quot;\" href=\"http://www.google.com/?a=1&amp;b=2\" target=\"_blank\"><b /><c /></a>\r\n<b class=\"abc\"></b>"
            );
        }

        [TestMethod]
        public void TestRenderBlobWithContent()
        {
            assertTranslated(
                "<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>&#x00D;&#x00A;</div>",
                "<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>\r\n</div>"
            );
        }

        [TestMethod]
        public void TestRenderBlobWithEmptyElements()
        {
            assertTranslated(
                "<x a=\"true\" />",
                "<x a=\"true\" />"
            );
        }

        [TestMethod]
        public void TestImportAbsolute()
        {
            var ce = getContentEngine();

            Blob blHeader = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Header</div>"));
            Blob blFooter = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Footer</div>"));
            Blob blTest = new Blob.Builder(Encoding.UTF8.GetBytes("<div><cms-import path=\"/template/header\" /><cms-import path=\"/template/footer\" /></div>"));
            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", blHeader.ID),
                    new TreeBlobReference.Builder("footer", blFooter.ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", blTest.ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the blob contents:
            var blTask = blrepo.PersistBlobs(new ImmutableContainer<BlobID, Blob>(bl => bl.ID, blHeader, blFooter, blTest));
            blTask.Wait();
            // Persist the trees:
            var trTask = trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));
            trTask.Wait();

            assertTranslated(
                ce,
                blTest,
                trRoot.ID,
                "<div><div>Header</div><div>Footer</div></div>"
            );
        }

        [TestMethod]
        public void TestImportRelative()
        {
            var ce = getContentEngine();

            Blob blHeader = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Header</div>"));
            Blob blFooter = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Footer</div>"));
            Blob blTest = new Blob.Builder(Encoding.UTF8.GetBytes("<div><cms-import path=\"../template/header\" /><cms-import path=\"../template/footer\" /></div>"));
            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", blHeader.ID),
                    new TreeBlobReference.Builder("footer", blFooter.ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", blTest.ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the blob contents:
            var blTask = blrepo.PersistBlobs(new ImmutableContainer<BlobID, Blob>(bl => bl.ID, blHeader, blFooter, blTest));
            blTask.Wait();
            // Persist the trees:
            var trTask = trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));
            trTask.Wait();

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/header", blHeader));
            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/footer", blFooter));
            assertTranslated(
                ce,
                new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/pages/test", blTest),
                "<div><div>Header</div><div>Footer</div></div>"
            );
        }

        [TestMethod]
        public void TestScheduled()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var ce = getContentEngine(a.AddDays(5));

            assertTranslated(
                ce,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                    a.ToString("u"),
                    b.ToString("u"),
                    c.ToString("u")
                ),
@"<div>
  Schedule content here!
</div>"
            );
        }

        [TestMethod]
        public void TestScheduled2()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var ce = getContentEngine(a.AddDays(5));

            assertTranslated(
                ce,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
    <else />
  </cms-scheduled>
</div>",
                    a.ToString("u"),
                    b.ToString("u"),
                    c.ToString("u")
                ),
@"<div>
  Schedule content here!
</div>"
            );
        }

        [TestMethod]
        public void TestScheduled3()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var ce = getContentEngine(a.AddDays(5));

            assertTranslated(
                ce,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content />
    <else>Not empty</else>
  </cms-scheduled>
</div>",
                    a.ToString("u"),
                    b.ToString("u"),
                    c.ToString("u")
                ),
@"<div>
  
</div>"
            );
        }

        [TestMethod]
        public void TestScheduledNot()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var ce = getContentEngine(a.AddDays(-5));

            assertTranslated(
                ce,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                    a.ToString("u"),
                    b.ToString("u"),
                    c.ToString("u")
                ),
@"<div>
  Else here?
</div>"
            );
        }

        [TestMethod]
        public void TestScheduleFail1()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""Hello World.""/>
    <range from=""Fail""/>
    <else>Else here?</else>
    <content>Schedule content here!</content>
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("could not parse 'from' attribute as a date/time", null, 0, 0),
                    new SemanticError("could not parse 'from' attribute as a date/time", null, 0, 0),
                    new SemanticError("'content' element must come before 'else' element", null, 0, 0),
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail2()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""2009-01-01"" to=""2008-01-01""/>
    <content />
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("'to' date must be later than 'from' date or empty", null, 0, 0),
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail3()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <content />
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("no 'range' elements found before 'content' element in cms-scheduled", null, 0, 0),
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail4()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""2009-01-01"" />
    <content />
    <content />
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("only one 'content' element may exist in cms-scheduled", null, 0, 0),
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail5()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""2009-01-01"">Should not contain content.</range>
    <content />
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("'range' element must be empty", null, 0, 0),
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail6()
        {
            // A <content /> node inside a non-empty <range> element should not count.
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""2009-01-01"">Should not contain content.<content /></range>
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("'range' element must be empty", null, 0, 0),
                    new SemanticError("no 'content' element found", null, 0, 0)
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail7()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""2009-01-01"" />
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("no 'content' element found", null, 0, 0)
                },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestScheduleFail8()
        {
            assumeFail(
@"<div>
  <cms-scheduled>
    <range from=""2009-01-01"" />
    <else>Need a content element</else>
  </cms-scheduled>
</div>",
                new SemanticError[] {
                    new SemanticError("'content' element must come before 'else' element", null, 0, 0),
                    new SemanticError("no 'content' element found", null, 0, 0)
                },
                new SemanticWarning[0]
            );
        }

        private class AEvaluator : IConditionalEvaluator
        {
            private bool setA;

            public AEvaluator(bool a)
            {
                setA = a;
            }

            #region IConditionalEvaluator Members

            public IConditionalEvaluator Next
            {
                get { return null; }
            }

            public EitherAndOr AndOr
            {
                get { return EitherAndOr.And; }
            }

            public bool EvaluateConditional(Dictionary<string, string> attributes)
            {
                string strA;
                bool testA;

                if (attributes.TryGetValue("a", out strA) && Boolean.TryParse(strA, out testA))
                {
                    return testA == setA;
                }

                return false;
            }

            #endregion
        }

        [TestMethod]
        public void TestConditionalIfElse_IfWins()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(true));

            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""true"">A is true!</if>
    <else>else A is false!</else>
  </cms-conditional>
</div>",
@"<div>
  A is true!
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalIfElse_ElseWins()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(false));

            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""true"">A is true!</if>
    <else>else A is false!</else>
  </cms-conditional>
</div>",
@"<div>
  else A is false!
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalIfElifElse_IfWins()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(true));

            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""true"">A is true!</if>
    <elif a=""false"">elif A is false!</elif>
    <else>else A is false!</else>
  </cms-conditional>
</div>",
@"<div>
  A is true!
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalIfElifElse_ElifWins()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(false));

            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""true"">A is true!</if>
    <elif a=""false"">elif A is false!</elif>
    <else>else A is false!</else>
  </cms-conditional>
</div>",
@"<div>
  elif A is false!
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalIfElifElse_ElseWins()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(false));

            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""true"">A is true!</if>
    <elif a=""not a bool"">elif A is not a bool!</elif>
    <else>else A is false!</else>
  </cms-conditional>
</div>",
@"<div>
  else A is false!
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalIfMultipleElifElse_ElifWins()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(false));

            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""true"">A is true!</if>
    <elif a=""garbage"">elif A is garbage!</elif>
    <elif a=""false"">elif A is false!</elif>
    <else>else A is false!</else>
  </cms-conditional>
</div>",
@"<div>
  elif A is false!
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalFail1()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(false));

            // if must be first.
            assumeFail(
                ce,
@"<div>
  <cms-conditional>
    <else>else A is false!</else>
    <elif a=""garbage"">elif A is garbage!</elif>
    <if a=""true"">A is true!</if>
  </cms-conditional>
</div>",
                new SemanticError[] { new SemanticError("expected 'if' element", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestConditionalFail2()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(false));

            // An if with no attributes is an error.
            assumeFail(
                ce,
@"<div>
  <cms-conditional>
    <if>Invalid if! Needs at least one conditional test attribute.</if>
  </cms-conditional>
</div>",
                new SemanticError[] { new SemanticError("expected at least one attribute for 'if' element", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestConditionalFail3()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(true));

            // An else with attributes is an error.
            assumeFail(
                ce,
@"<div>
  <cms-conditional>
    <if a=""false"">A is false!</if>
    <else a=""true"">Bad else condition! Must have no attributes.</else>
  </cms-conditional>
</div>",
                new SemanticError[] { new SemanticError("unexpected attributes on 'else' element", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestConditionalOkay1()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(true));

            // An if without an else is okay.
            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <if a=""false"">A is false!</if>
  </cms-conditional>
</div>",
@"<div>
  
</div>"
            );
        }

        [TestMethod]
        public void TestConditionalOkay2()
        {
            var ce = getContentEngine(evaluator: new AEvaluator(true));

            // Text and comments are ignored around the 'if', 'elif', and 'else' elements.
            assertTranslated(
                ce,
@"<div>
  <cms-conditional>
    <!-- documentation here. -->

    <if a=""false"">A is false!</if>

Well that was fun!

    This is unnecessary extra text that is ignored.
  </cms-conditional>
</div>",
@"<div>
  
</div>"
            );
        }

        [TestMethod]
        public void TestCMSLinkAbsolute()
        {
            assertTranslated(
@"<div>
  <cms-link path=""/hello/world"">Link text.</cms-link>
</div>",
@"<div>
  <a href=""/hello/world"">Link text.</a>
</div>"
            );
        }

        [TestMethod]
        public void TestCMSLinkAttributes()
        {
            assertTranslated(
@"<div>
  <cms-link path=""/hello/world"" target=""_blank"">Link text.</cms-link>
</div>",
@"<div>
  <a href=""/hello/world"" target=""_blank"">Link text.</a>
</div>"
            );
        }

        [TestMethod]
        public void TestCMSLinkRelative()
        {
            assertTranslated(
@"<div>
  <cms-link path=""dumb/../hello/world"" target=""_blank"">Link text.</cms-link>
</div>",
@"<div>
  <a href=""/hello/world"" target=""_blank"">Link text.</a>
</div>"
            );
        }

        [TestMethod]
        public void TestCMSLinkEmpty()
        {
            assertTranslated(
@"<div>
  <cms-link path=""/hello/world"" target=""_blank"" />
</div>",
@"<div>
  <a href=""/hello/world"" target=""_blank"" />
</div>"
            );
        }

        [TestMethod]
        public void TestCMSLinkFail1()
        {
            assumeFail(
@"<div>
  <cms-link />
</div>",
                new SemanticError[] { new SemanticError("cms-link has no attributes", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestCMSLinkFail1a()
        {
            assumeFail(
@"<div>
  <cms-link>Hello world.</cms-link>
</div>",
                new SemanticError[] { new SemanticError("cms-link has no attributes", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        [TestMethod]
        public void TestCMSLinkFail2()
        {
            assumeFail(
@"<div>
  <cms-link target=""_blank"" />
</div>",
                new SemanticError[0],
                new SemanticWarning[] { new SemanticWarning("expected 'path' attribute on 'cms-link' element was not found", null, 0, 0) }
            );
        }

        [TestMethod]
        public void TestCMSLinkFail2a()
        {
            assumeFail(
@"<div>
  <cms-link target=""_blank"">Hello world.</cms-link>
</div>",
                new SemanticError[0],
                new SemanticWarning[] { new SemanticWarning("expected 'path' attribute on 'cms-link' element was not found", null, 0, 0) }
            );
        }

        [TestMethod]
        public void TestNestedElements()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var ce = getContentEngine(a.AddDays(5));

            Blob blHeader = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Header</div>"));
            Blob blFooter = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Footer</div>"));
            Blob blTest = new Blob.Builder(Encoding.UTF8.GetBytes(String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content><cms-import path=""/template/header"" />In between content.<cms-import path=""/template/footer"" /></content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                a.ToString("u"),
                b.ToString("u"),
                c.ToString("u")
            )));

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", blHeader.ID),
                    new TreeBlobReference.Builder("footer", blFooter.ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", blTest.ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the blob contents:
            var blTask = blrepo.PersistBlobs(new ImmutableContainer<BlobID, Blob>(bl => bl.ID, blHeader, blFooter, blTest));
            blTask.Wait();
            // Persist the trees:
            var trTask = trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));
            trTask.Wait();

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/header", blHeader));
            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/footer", blFooter));
            assertTranslated(
                ce,
                blTest,
                trRoot.ID,
@"<div>
  <div>Header</div>In between content.<div>Footer</div>
</div>"
            );
        }

        [TestMethod]
        public void TestNestedElementsSkip()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a - 5 days as the viewing date for scheduling:
            var ce = getContentEngine(a.AddDays(-5));

            Blob blHeader = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Header</div>"));
            Blob blFooter = new Blob.Builder(Encoding.UTF8.GetBytes("<div>Footer</div>"));
            Blob blTest = new Blob.Builder(Encoding.UTF8.GetBytes(String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content><cms-import path=""/template/header"" />In between content.<cms-import path=""/template/footer"" /></content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                a.ToString("u"),
                b.ToString("u"),
                c.ToString("u")
            )));

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", blHeader.ID),
                    new TreeBlobReference.Builder("footer", blFooter.ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", blTest.ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the blob contents:
            var blTask = blrepo.PersistBlobs(new ImmutableContainer<BlobID, Blob>(bl => bl.ID, blHeader, blFooter, blTest));
            blTask.Wait();
            // Persist the trees:
            var trTask = trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));
            trTask.Wait();

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/header", blHeader));
            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/footer", blFooter));
            assertTranslated(
                ce,
                blTest,
                trRoot.ID,
@"<div>
  Else here?
</div>"
            );
        }

        [TestMethod]
        public void TestUnknownSkipped()
        {
            assertTranslated(
                "<div><cms-unknown crap=\"some stuff\"><custom-tag>Skipped stuff.</custom-tag>Random gibberish that will be removed.</cms-unknown></div>",
                "<div></div>"
            );
        }
    }
}
