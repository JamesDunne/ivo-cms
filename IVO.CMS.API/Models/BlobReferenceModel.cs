using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;

namespace IVO.CMS.API.Models
{
    [Serializable]
    public sealed class BlobReferenceModel
    {
        public string Name { get; set; }
        public BlobID BlobID { get; set; }
    }
}
