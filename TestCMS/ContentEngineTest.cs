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
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a><b/><c/></a>
<b></b>"));
            var ce = getContentEngine();
            var frag = ce.RenderBlob(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            output(frag);
        }

        [TestMethod]
        public void TestRenderBlobAttributes()
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a style=""color: &amp;too&quot;here&quot;"" href=""http://www.google.com/?a=1&amp;b=2"" target=""_blank""><b/><c/></a>
<b class=""abc""></b>"));
            var ce = getContentEngine();
            var frag = ce.RenderBlob(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            output(frag);
        }

        [TestMethod]
        public void TestRenderBlobWithContent()
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>&#x00D;&#x00A;</div>"));
            var ce = getContentEngine();
            var frag = ce.RenderBlob(new CanonicalizedAbsolutePath("test"), new TreeID(), bl);
            output(frag);
        }
    }
}
