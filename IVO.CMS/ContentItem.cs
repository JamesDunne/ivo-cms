using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;

namespace IVO.CMS
{
    public sealed class ContentItem
    {
        public ContentItem(CanonicalizedAbsolutePath path, TreeID rootid, Blob bl)
        {
            this.Path = path;
            this.RootTreeID = rootid;
            this.Blob = bl;
        }

        public CanonicalizedAbsolutePath Path { get; private set; }
        public TreeID RootTreeID { get; private set; }
        public Blob Blob { get; private set; }
    }
}
