using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using IVO.Definition.Containers;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TreeRequest
    {
        public TreeBlobRefRequest[] blobs { get; set; }
        public TreeTreeRefRequest[] trees { get; set; }
    }
}
