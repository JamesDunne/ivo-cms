using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IVO.Definition.Models;
using IVO.CMS;

namespace TestCMS
{
    [TestClass]
    public class ContentEngineTest
    {
        private void output(HTMLFragment fragment)
        {
            Console.WriteLine(((string)fragment).Replace("\n", Environment.NewLine));
        }

        [TestMethod]
        public void TestRenderBlob()
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a><b/><c/></a>
<b></b>"));
            var ce = new ContentEngine();
            var frag = ce.RenderBlob(bl);
            output(frag);
        }

        [TestMethod]
        public void TestRenderBlobAttributes()
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<a style=""color: &amp;too&quot;here&quot;"" href=""http://www.google.com/?a=1&amp;b=2"" target=""_blank""><b/><c/></a>
<b class=""abc""></b>"));
            var ce = new ContentEngine();
            var frag = ce.RenderBlob(bl);
            output(frag);
        }

        [TestMethod]
        public void TestRenderBlobWithContent()
        {
            Blob bl = new Blob.Builder(Encoding.UTF8.GetBytes(@"<div><p>Some content &amp; stuff here. Maybe some &lt; entities &gt; and such?</p>&#x00D;&#x00A;</div>"));
            var ce = new ContentEngine();
            var frag = ce.RenderBlob(bl);
            output(frag);
        }
    }
}
