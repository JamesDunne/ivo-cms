using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;

namespace IVO.CMS
{
    public sealed class SemanticWarning
    {
        public SemanticWarning(string message, TreePathStreamedBlob item, int lineNumber, int linePosition)
        {
            this.Message = message;
            this.Item = item;
            this.LineNumber = lineNumber;
            this.LinePosition = linePosition;
        }

        public string Message { get; private set; }
        public TreePathStreamedBlob Item { get; private set; }
        public int LineNumber { get; private set; }
        public int LinePosition { get; private set; }
    }
}
