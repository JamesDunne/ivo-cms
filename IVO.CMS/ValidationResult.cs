using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IVO.CMS
{
    public sealed class ValidationResult
    {
        public ValidationResult(string message, int lineNumber, int linePosition)
        {
            this.Message = message;
            this.LineNumber = lineNumber;
            this.LinePosition = linePosition;
        }

        public string Message { get; private set; }
        public int LineNumber { get; private set; }
        public int LinePosition { get; private set; }
    }
}
