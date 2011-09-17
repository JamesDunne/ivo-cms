using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using IVO.Definition.Containers;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class TreeModel
    {
        public BlobReferenceModel[] Blobs { get; set; }
        public TreeReferenceModel[] Trees { get; set; }
    }
}
