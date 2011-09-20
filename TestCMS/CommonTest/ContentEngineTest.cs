using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Asynq;
using IVO.CMS;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Containers;
using IVO.Definition.Models;
using IVO.Definition.Repositories;
using IVO.Implementation.SQL;
using IVO.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace TestCMS.CommonTest
{
    public class ContentEngineTestMethods : CommonTestBase
    {
        public ContentEngineTestMethods(GetTestContextDelegate getTestContext)
        {
            this.getTestContext = getTestContext;
        }

        public void TestRenderBlob()
        {
            assertTranslated(
                "<a><b/><c/></a>\r\n<b></b>",
                "<a><b /><c /></a>\r\n<b></b>"
            );
        }

        public void TestRenderBlobAttributes()
        {
            assertTranslated(
                "<a style=\"color: &amp;too&quot;here&quot;\" href=\"http://www.google.com/?a=1&amp;b=2\" target=\"_blank\"><b /><c /></a>\r\n<b class=\"abc\"></b>",
                "<a style=\"color: &amp;too&quot;here&quot;\" href=\"http://www.google.com/?a=1&amp;b=2\" target=\"_blank\"><b /><c /></a>\r\n<b class=\"abc\"></b>"
            );
        }

        public void TestRenderBlobWithContent()
        {
            assertTranslated(
                "<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>&#x00D;&#x00A;</div>",
                "<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>\r\n</div>"
            );
        }

        public void TestRenderBlobWithEmptyElements()
        {
            assertTranslated(
                "<x a=\"true\" />",
                "<x a=\"true\" />"
            );
        }

        public async Task TestImportAbsolute()
        {
            var tc = getTestContext();

            PersistingBlob blHeader = new PersistingBlob("<div>Header</div>".ToStream());
            PersistingBlob blFooter = new PersistingBlob("<div>Footer</div>".ToStream());
            PersistingBlob blTest = new PersistingBlob("<div><cms-import path=\"/template/header\" /><cms-import path=\"/template/footer\" /></div>".ToStream());

            // Persist the blob contents:
            var sblobs = await tc.blrepo.PersistBlobs(blHeader, blFooter, blTest);

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                        new TreeBlobReference.Builder("header", sblobs[0].ID),
                        new TreeBlobReference.Builder("footer", sblobs[1].ID)
                    }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                        new TreeBlobReference.Builder("test", sblobs[2].ID)
                    }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                        new TreeTreeReference.Builder("template", trTemplate.ID),
                        new TreeTreeReference.Builder("pages", trPages.ID)
                    },
                new List<TreeBlobReference>(0)
            );

            // Persist the trees:
            var trTask = await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));

            assertTranslated(
                tc,
                sblobs[2],   // aka blTest
                trRoot.ID,
                "<div><div>Header</div><div>Footer</div></div>"
            );
        }

        public async Task TestImportRelative()
        {
            var tc = getTestContext();

            PersistingBlob blHeader = new PersistingBlob("<div>Header</div>".ToStream());
            PersistingBlob blFooter = new PersistingBlob("<div>Footer</div>".ToStream());
            PersistingBlob blTest = new PersistingBlob("<div><cms-import path=\"../template/header\" /><cms-import path=\"../template/footer\" /></div>".ToStream());

            // Persist the blob contents:
            var sblobs = await tc.blrepo.PersistBlobs(blHeader, blFooter, blTest);

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", sblobs[0].ID),
                    new TreeBlobReference.Builder("footer", sblobs[1].ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", sblobs[2].ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the trees:
            var trTask = await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/header", sblobs[0]));
            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/footer", sblobs[1]));
            assertTranslated(
                tc,
                new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/pages/test", sblobs[2]),
                "<div><div>Header</div><div>Footer</div></div>"
            );
        }

        private async Task testImportTemplate(string templateMain, string pagesTest, string expected, params SemanticWarning[] expectedWarnings)
        {
            var tc = getTestContext();

            PersistingBlob blHeader = new PersistingBlob(templateMain.ToStream());
            PersistingBlob blTest = new PersistingBlob(pagesTest.ToStream());

            // Persist the blob contents:
            var sblobs = await tc.blrepo.PersistBlobs(blHeader, blTest);

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("main", sblobs[0].ID),
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", sblobs[1].ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the trees:
            var trTask = await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/templates/main", sblobs[0]));
            assertTranslated(tc, new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/pages/test", sblobs[1]), expected, expectedWarnings);
        }

        private async Task testImportTemplateFail(string templateMain, string pagesTest, SemanticError[] expectedErrors, SemanticWarning[] expectedWarnings)
        {
            var tc = getTestContext();

            PersistingBlob blHeader = new PersistingBlob(templateMain.ToStream());
            PersistingBlob blTest = new PersistingBlob(pagesTest.ToStream());

            // Persist the blob contents:
            var sblobs = await tc.blrepo.PersistBlobs(blHeader, blTest);

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("main", sblobs[0].ID),
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", sblobs[1].ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the trees:
            var trTask = await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/templates/main", sblobs[0]));
            assumeFail(tc, new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/pages/test", sblobs[1]), expectedErrors, expectedWarnings);
        }

        public Task TestImportTemplateAbsolute()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
    <body><cms-template-area id=""body""/></body>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body"">Body</area>
</cms-import-template>",
@"<html>
    <head></head>
    <body>Body</body>
</html>"
            );
        }

        public Task TestImportTemplateAbsoluteExtra()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
    <body><cms-template-area id=""pre-body""/><cms-template-area id=""body""/></body>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body"">Body</area>
</cms-import-template>",
@"<html>
    <head></head>
    <body>Body</body>
</html>"
            );
        }

        public Task TestImportTemplateAbsoluteDefault()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
    <body><cms-template-area id=""pre-body"">Pre-Body </cms-template-area><cms-template-area id=""body""/></body>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body"">Body</area>
</cms-import-template>",
@"<html>
    <head></head>
    <body>Pre-Body Body</body>
</html>"
            );
        }

        public Task TestImportTemplateAbsoluteNestedArea()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
    <body><cms-template-area id=""body"">Before inner.<cms-template-area id=""body-inner""/>After inner.</cms-template-area></body>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body-inner"">Body inner.</area>
</cms-import-template>",
@"<html>
    <head></head>
    <body>Before inner.Body inner.After inner.</body>
</html>"
            );
        }

        public Task TestImportTemplateAbsoluteNestedAreaDefault()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
    <body><cms-template-area id=""body"">Before inner.<cms-template-area id=""body-inner""/>After inner.</cms-template-area></body>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body"">Body.</area>
</cms-import-template>",
@"<html>
    <head></head>
    <body>Body.</body>
</html>"
            );
        }

        public Task TestImportTemplateAbsoluteNoFiller()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
    <body><cms-template-area id=""body"">Before inner.<cms-template-area id=""body-inner""/>After inner.</cms-template-area></body>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""dumb""></area>
    <area id=""world""></area>
</cms-import-template>",
@"<html>
    <head></head>
    <body>Before inner.After inner.</body>
</html>",
                new SemanticWarning("area 'dumb' unused by the template", null, 0, 0),
                new SemanticWarning("area 'world' unused by the template", null, 0, 0)
            );
        }

        public Task TestImportTemplateFail1()
        {
            return testImportTemplateFail(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
</html></cms-template>",
@"<cms-import-template>
    <area id=""head""></area>
    <area id=""body"">Body.</area>
</cms-import-template>",
                new SemanticError[] { new SemanticError("cms-import-template requires a 'path' attribute", null, 0, 0) },
                new SemanticWarning[] { }
            );
        }

        public Task TestImportTemplateFail2()
        {
            return testImportTemplateFail(
@"<cms-template><html>
    <head><cms-template-area id=""head""/></head>
</html></cms-template>",
@"<cms-import-template path=""fail"">
    <area id=""head""></area>
    <area id=""body"">Body.</area>
</cms-import-template>",
                new SemanticError[] { new SemanticError("cms-import-template path '/pages/fail' not found", null, 0, 0) },
                new SemanticWarning[] { }
            );
        }

        public Task TestImportTemplateFail3()
        {
            return testImportTemplateFail(
@"<html>
    <head><cms-template-area id=""head""/></head>
</html>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body"">Body.</area>
</cms-import-template>",
                new SemanticError[] { new SemanticError("cms-import-template expected cms-template as first element of imported template", null, 0, 0) },
                new SemanticWarning[] { }
            );
        }

        public Task TestImportTemplateFail4()
        {
            return testImportTemplateFail(
@"<cms-template><html>
    <head><cms-template-area /></head>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""></area>
    <area id=""body"">Body.</area>
</cms-import-template>",
                new SemanticError[] { new SemanticError("cms-template-area needs an 'id' attribute", null, 0, 0) },
                new SemanticWarning[] {
                    new SemanticWarning("area 'head' unused by the template", null, 0, 0),
                    new SemanticWarning("area 'body' unused by the template", null, 0, 0)
                }
            );
        }

        public Task TestImportTemplateFail5()
        {
            return testImportTemplateFail(
@"<cms-template><html>
    <head><cms-template-area id=""head"" /></head>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <div>This can't be here.</div>
</cms-import-template>",
                new SemanticError[] { new SemanticError("cms-import-template may only contain 'area' elements", null, 0, 0) },
                new SemanticWarning[] { }
            );
        }

        public Task TestImportTemplateFail6()
        {
            return testImportTemplateFail(
@"<cms-template><html>
    <head><cms-template-area id=""head"" /></head>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area>This needs an 'id' attribute.</area>
</cms-import-template>",
                new SemanticError[] { new SemanticError("area element must have an 'id' attribute", null, 0, 0) },
                new SemanticWarning[] { }
            );
        }

        public Task TestImportTemplateFillerNestedCustomElements()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head"" /></head>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
    <area id=""head""><cms-conditional>
        <if true=""false"">Not this</if>
        <else>Yeah!</else>
    </cms-conditional></area>
</cms-import-template>",
@"<html>
    <head>Yeah!</head>
</html>"
            );
        }

        public Task TestImportTemplateTemplateNestedCustomElements()
        {
            return testImportTemplate(
@"<cms-template><html>
    <head><cms-template-area id=""head""><cms-conditional>
        <if true=""false"">Not this</if>
        <else>Yeah!</else>
    </cms-conditional></cms-template-area></head>
</html></cms-template>",
@"<cms-import-template path=""/template/main"">
</cms-import-template>",
@"<html>
    <head>Yeah!</head>
</html>"
            );
        }

        public void TestScheduled()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(5));

            assertTranslated(
                tc,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                    a.ToString(),
                    b.ToString(),
                    c.ToString()
                ),
@"<div>
  Schedule content here!
</div>"
            );
        }

        public void TestScheduled2()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(5));

            assertTranslated(
                tc,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
    <else />
  </cms-scheduled>
</div>",
                    a.ToString(),
                    b.ToString(),
                    c.ToString()
                ),
@"<div>
  Schedule content here!
</div>"
            );
        }

        public void TestScheduled3()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(5));

            assertTranslated(
                tc,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content />
    <else>Not empty</else>
  </cms-scheduled>
</div>",
                    a.ToString(),
                    b.ToString(),
                    c.ToString()
                ),
@"<div>
  
</div>"
            );
        }

        public void TestScheduledNot()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(-5));

            assertTranslated(
                tc,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                    a.ToString(),
                    b.ToString(),
                    c.ToString()
                ),
@"<div>
  Else here?
</div>"
            );
        }

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

        public void TestConditionalIfElse_IfWins()
        {
            var tc = getTestContext(evaluator: new AEvaluator(true));

            assertTranslated(
                tc,
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

        public void TestConditionalIfElse_ElseWins()
        {
            var tc = getTestContext(evaluator: new AEvaluator(false));

            assertTranslated(
                tc,
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

        public void TestConditionalIfElifElse_IfWins()
        {
            var tc = getTestContext(evaluator: new AEvaluator(true));

            assertTranslated(
                tc,
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

        public void TestConditionalIfElifElse_ElifWins()
        {
            var tc = getTestContext(evaluator: new AEvaluator(false));

            assertTranslated(
                tc,
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

        public void TestConditionalIfElifElse_ElseWins()
        {
            var tc = getTestContext(evaluator: new AEvaluator(false));

            assertTranslated(
                tc,
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

        public void TestConditionalIfMultipleElifElse_ElifWins()
        {
            var tc = getTestContext(evaluator: new AEvaluator(false));

            assertTranslated(
                tc,
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

        public void TestConditionalFail1()
        {
            var tc = getTestContext(evaluator: new AEvaluator(false));

            // if must be first.
            assumeFail(
                tc,
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

        public void TestConditionalFail2()
        {
            var tc = getTestContext(evaluator: new AEvaluator(false));

            // An if with no attributes is an error.
            assumeFail(
                tc,
@"<div>
  <cms-conditional>
    <if>Invalid if! Needs at least one conditional test attribute.</if>
  </cms-conditional>
</div>",
                new SemanticError[] { new SemanticError("expected at least one attribute for 'if' element", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        public void TestConditionalFail3()
        {
            var tc = getTestContext(evaluator: new AEvaluator(true));

            // An else with attributes is an error.
            assumeFail(
                tc,
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

        public void TestConditionalOkay1()
        {
            var tc = getTestContext(evaluator: new AEvaluator(true));

            // An if without an else is okay.
            assertTranslated(
                tc,
@"<div>
  <cms-conditional>
    <if a=""false"">A is false!</if>
  </cms-conditional>
</div>",
@"<div>
  
</div>"
            );
        }

        public void TestConditionalOkay2()
        {
            var tc = getTestContext(evaluator: new AEvaluator(true));

            // Text and comments are ignored around the 'if', 'elif', and 'else' elements.
            assertTranslated(
                tc,
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

        public void TestCMSLinkFail2()
        {
            assumeFail(
@"<div>
  <cms-link>Hello world.</cms-link>
</div>",
                new SemanticError[] { new SemanticError("cms-link has no attributes", null, 0, 0) },
                new SemanticWarning[0]
            );
        }

        public void TestCMSLinkFail3()
        {
            assumeFail(
@"<div>
  <cms-link target=""_blank"" />
</div>",
                new SemanticError[0],
                new SemanticWarning[] { new SemanticWarning("expected 'path' attribute on 'cms-link' element was not found", null, 0, 0) }
            );
        }

        public void TestCMSLinkFail4()
        {
            assumeFail(
@"<div>
  <cms-link target=""_blank"">Hello world.</cms-link>
</div>",
                new SemanticError[0],
                new SemanticWarning[] { new SemanticWarning("expected 'path' attribute on 'cms-link' element was not found", null, 0, 0) }
            );
        }

        public async Task TestNestedElements()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(5));

            PersistingBlob blHeader = new PersistingBlob("<div>Header</div>".ToStream());
            PersistingBlob blFooter = new PersistingBlob("<div>Footer</div>".ToStream());
            PersistingBlob blTest = new PersistingBlob(String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content><cms-import path=""/template/header"" />In between content.<cms-import path=""/template/footer"" /></content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                a.ToString(),
                b.ToString(),
                c.ToString()
            ).ToStream());

            // Persist the blob contents:
            var sblobs = await tc.blrepo.PersistBlobs(blHeader, blFooter, blTest);

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", sblobs[0].ID),
                    new TreeBlobReference.Builder("footer", sblobs[1].ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", sblobs[2].ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the trees:
            var trTask = await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/header", sblobs[0]));
            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/footer", sblobs[1]));
            assertTranslated(
                tc,
                sblobs[2],
                trRoot.ID,
@"<div>
  <div>Header</div>In between content.<div>Footer</div>
</div>"
            );
        }

        public async Task TestNestedElementsSkip()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a - 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(-5));

            PersistingBlob blHeader = new PersistingBlob("<div>Header</div>".ToStream());
            PersistingBlob blFooter = new PersistingBlob("<div>Footer</div>".ToStream());
            PersistingBlob blTest = new PersistingBlob(String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content><cms-import path=""/template/header"" />In between content.<cms-import path=""/template/footer"" /></content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                a.ToString(),
                b.ToString(),
                c.ToString()
            ).ToStream());

            // Persist the blob contents:
            var sblobs = await tc.blrepo.PersistBlobs(blHeader, blFooter, blTest);

            Tree trTemplate = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", sblobs[0].ID),
                    new TreeBlobReference.Builder("footer", sblobs[1].ID)
                }
            );
            Tree trPages = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("test", sblobs[2].ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> {
                    new TreeTreeReference.Builder("template", trTemplate.ID),
                    new TreeTreeReference.Builder("pages", trPages.ID)
                },
                new List<TreeBlobReference>(0)
            );

            // Persist the trees:
            var trTask = await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trTemplate, trPages, trRoot));

            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/header", sblobs[0]));
            output(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/template/footer", sblobs[1]));
            assertTranslated(
                tc,
                sblobs[2],
                trRoot.ID,
@"<div>
  Else here?
</div>"
            );
        }

        public void TestUnknownSkipped()
        {
            assertTranslated(
                "<div><cms-unknown crap=\"some stuff\"><custom-tag>Skipped stuff.</custom-tag>Random gibberish that will be removed.</cms-unknown></div>",
                "<div></div>",
                new SemanticWarning("No custom element providers processed unknown element, 'cms-unknown'; skipping its contents entirely.", null, 0, 0)
            );
        }

        public async Task SpeedTestRenderBlob()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use a + 5 days as the viewing date for scheduling:
            var tc = getTestContext(a.AddDays(5));

            string tmp = Path.GetTempFileName();

            using (var fs = new FileStream(tmp, FileMode.Open, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                for (int i = 0; i < 16000; ++i)
                {
                    //<cms-import path=""/template/header"" />In between content.<cms-import path=""/template/footer"" />
                    sw.WriteLine(String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content><cms-import path=""/template/header"" />In between content.<cms-import path=""/template/footer"" /></content>
    <else>Else here?</else>
  </cms-scheduled>
</div>",
                        a.ToString(),
                        b.ToString(),
                        c.ToString()
                    ));
                }
            }

            var pblHeader = new PersistingBlob("HEADER".ToStream());
            var pblFooter = new PersistingBlob("FOOTER".ToStream());
            var pblTest = new PersistingBlob(new FileStream(tmp, FileMode.Open, FileAccess.Read, FileShare.Read));

            var bls = await tc.blrepo.PersistBlobs(pblHeader, pblFooter, pblTest);

            Tree trTmpl = new Tree.Builder(
                new List<TreeTreeReference>(0),
                new List<TreeBlobReference> {
                    new TreeBlobReference.Builder("header", bls[0].ID),
                    new TreeBlobReference.Builder("footer", bls[1].ID)
                }
            );
            Tree trRoot = new Tree.Builder(
                new List<TreeTreeReference> { new TreeTreeReference.Builder("template", trTmpl.ID) },
                new List<TreeBlobReference> { new TreeBlobReference.Builder("test", bls[2].ID) }
            );

            await tc.trrepo.PersistTree(trRoot.ID, new ImmutableContainer<TreeID, Tree>(tr => tr.ID, trRoot, trTmpl));

            Stopwatch stpw = Stopwatch.StartNew();
            await tc.ce.RenderBlob(new TreePathStreamedBlob(trRoot.ID, (CanonicalBlobPath)"/test", bls[2]));
            stpw.Stop();

            var errs = tc.ce.GetErrors();
            Console.WriteLine("Errors: {0}", errs.Count);
            for (int i = 0; i < 10 && i < errs.Count; ++i)
            {
                Console.WriteLine("  {0}", errs[i].ToString());
            }
            Console.WriteLine();
            Console.WriteLine("Time: {0} ms", stpw.ElapsedMilliseconds);
        }
    }
}
