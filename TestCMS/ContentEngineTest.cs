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

namespace TestCMS
{
    [TestClass]
    public class ContentEngineTest
    {
        private void output(HTMLFragment fragment)
        {
            Console.WriteLine(((string)fragment).Replace("\n", Environment.NewLine));
        }

        private DataContext getDataContext()
        {
            return new DataContext(@"Data Source=.\SQLEXPRESS;Initial Catalog=IVO;Integrated Security=SSPI");
        }

        private ContentEngine getContentEngine(DateTimeOffset? viewDate = null)
        {
            DateTimeOffset realDate = viewDate ?? DateTimeOffset.Now;

            var db = getDataContext();
            ITreeRepository trrepo = new TreeRepository(db);
            IBlobRepository blrepo = new BlobRepository(db);
            return new ContentEngine(trrepo, blrepo, realDate);
        }

        private void assertTranslated(string blob, string expected)
        {
            var ce = getContentEngine();
            assertTranslated(ce, blob, expected);
        }

        private void assertTranslated(ContentEngine ce, string blob, string expected)
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(blob));
            output((HTMLFragment)Encoding.UTF8.GetString(bl.Contents));
            output((HTMLFragment)"-----------------------------------------");

            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);

            foreach (var err in ce.GetErrors())
            {
                Console.Error.WriteLine("{0} ({1}:{2}): {3}", err.Item.Path, err.LineNumber, err.LinePosition, err.Message);
            }

            Assert.AreEqual(expected, (string)frag);
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
                "<a style=\"color: &amp;too&quot;here&quot;\" href=\"http://www.google.com/?a=1&amp;b=2\" target=\"_blank\"><b/><c/></a>\r\n<b class=\"abc\"></b>",
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
        public void TestImport()
        {
            assertTranslated(
                "<div><cms-import absolute-path=\"/template/head.html\" /></div>",
                "<div></div>"
            );
        }

        [TestMethod]
        public void TestScheduled()
        {
            DateTimeOffset a = new DateTimeOffset(2011, 09, 1, 0, 0, 0, 0, TimeSpan.FromHours(-5));
            DateTimeOffset b = a.AddDays(15);
            DateTimeOffset c = a.AddDays(30);
            // Use b + 5 days as the viewing date for scheduling:
            var ce = getContentEngine(b.AddDays(5));

            assertTranslated(
                ce,
                String.Format(
@"<div>
  <cms-scheduled>
    <range from=""{0}"" to=""{2}""/>
    <range from=""{1}"" to=""{2}""/>
    <content>Schedule content here!</content>
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
        public void TestUnknownSkipped()
        {
            assertTranslated(
                "<div><cms-unknown crap=\"some stuff\"><custom-tag>Skipped stuff.</custom-tag>Random gibberish that will be removed.</cms-unknown></div>",
                "<div></div>"
            );
        }
    }
}
