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

        private ContentEngine getContentEngine()
        {
            var db = getDataContext();
            ITreeRepository trrepo = new TreeRepository(db);
            IBlobRepository blrepo = new BlobRepository(db);
            return new ContentEngine(trrepo, blrepo);
        }

        [TestMethod]
        public void TestRenderBlob()
        {
            var ce = getContentEngine();
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a><b/><c/></a>
<b></b>"));
            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);
            Assert.AreEqual("<a><b /><c /></a>\n<b></b>", (string)frag);
        }

        [TestMethod]
        public void TestRenderBlobAttributes()
        {
            var ce = getContentEngine();
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a style=""color: &amp;too&quot;here&quot;"" href=""http://www.google.com/?a=1&amp;b=2"" target=""_blank""><b/><c/></a>
<b class=""abc""></b>"));
            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);
            Assert.AreEqual("<a style=\"color: &amp;too&quot;here&quot;\" href=\"http://www.google.com/?a=1&amp;b=2\" target=\"_blank\"><b /><c /></a>\n<b class=\"abc\"></b>", (string)frag);
        }

        [TestMethod]
        public void TestRenderBlobWithContent()
        {
            var ce = getContentEngine();
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>&#x00D;&#x00A;</div>"));
            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);
            Assert.AreEqual("<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>\r\n</div>", (string)frag);
        }

        [TestMethod]
        public void TestImport()
        {
            var ce = getContentEngine();
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<div><cms-import absolute-path=""/template/head.html"" /></div>"));
            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);
            Assert.AreEqual(@"<div></div>", (string)frag);
        }

        [TestMethod]
        public void TestScheduled()
        {
            var ce = getContentEngine();
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<div><cms-scheduled><range from="""" to=""""/><content>Schedule content here!</content></cms-scheduled></div>"));
            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);
            Assert.AreEqual(@"<div></div>", (string)frag);
        }

        [TestMethod]
        public void TestUnknownSkipped()
        {
            var ce = getContentEngine();
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<div><cms-unknown crap=""some stuff""><custom-tag>Skipped stuff.</custom-tag>Random gibberish that will be removed.</cms-unknown></div>"));
            var item = new ContentItem(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            var frag = ce.RenderContentItem(item);
            output(frag);
            Assert.AreEqual(@"<div></div>", (string)frag);
        }
    }
}
