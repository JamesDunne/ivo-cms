using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IVO.Definition.Models;
using System.Xml.Linq;
using System.Xml;

namespace IVO.CMS
{
    public sealed class ContentEngine
    {
        public HTMLFragment RenderBlob(Blob bl)
        {
            // Load the XML from the blob that is encoded as UTF-8:
            XDocument xd = XDocument.Parse(Encoding.UTF8.GetString(bl.Contents));
            // Create a string builder used to build the output polyglot HTML5 document fragment:
            StringBuilder sb = new StringBuilder(bl.Contents.Length);

            Stack<XNode> stk = new Stack<XNode>();
            stk.Push(xd);
            while (stk.Count > 0)
            {
                XNode n = stk.Pop();
                XElement xe;

                switch (n.NodeType)
                {
                    case XmlNodeType.Element:
                        xe = (XElement)n;
                        sb.AppendFormat("<{0}", xe.Name);
                        if (xe.HasAttributes)
                        {
                            sb.Append(String.Join(" ", from at in xe.Attributes() select at.ToString()));
                        }
                        if (xe.IsEmpty)
                        {
                            sb.Append(" />");
                            break;
                        }
                        break;
                    case XmlNodeType.EndElement:
                        xe = (XElement)n;
                        sb.AppendFormat("</{0}>", xe.Name);
                        break;
                    default:
                        break;
                }
            }

            string result = sb.ToString();
            return new HTMLFragment(result);
        }
    }
}
