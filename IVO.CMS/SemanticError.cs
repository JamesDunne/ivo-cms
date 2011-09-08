using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;

namespace IVO.CMS
{
    public sealed class SemanticError : Exception
    {
        public SemanticError(string message, TreePathBlob item, int lineNumber, int linePosition) : base(message)
        {
            this.Item = item;
            this.LineNumber = lineNumber;
            this.LinePosition = linePosition;
        }

        public TreePathBlob Item { get; private set; }
        public int LineNumber { get; private set; }
        public int LinePosition { get; private set; }
    }
}
