using System;
using System.IO;
using System.Text;
using System.Xml;
using IVO.Definition.Models;
using System.Web;

namespace IVO.CMS
{
    public sealed class ContentEngine
    {
        private const string rootElementName = "_root_";

        private static readonly byte[] rootOpen = Encoding.UTF8.GetBytes("<" + rootElementName + ">");
        private static readonly byte[] rootClose = Encoding.UTF8.GetBytes("</" + rootElementName +">");

        public HTMLFragment RenderBlob(Blob bl)
        {
            // NOTE: I would much prefer to load in a Stream from the persistence store rather than a `byte[]`.

            // Create a string builder used to build the output polyglot HTML5 document fragment:
            StringBuilder sb = new StringBuilder(bl.Contents.Length);

            // NOTE: this effectively limits us to 2GB documents due to the use of `int`s, but I don't think
            // that's really a big thing to worry about in a web-based CMS.

            // Hack to allow a document fragment:
            byte[] rootedContents = new byte[bl.Contents.Length + rootOpen.Length + rootClose.Length];
            Array.Copy(rootOpen, 0, rootedContents, 0, rootOpen.Length);
            Array.Copy(bl.Contents, 0, rootedContents, rootOpen.Length, bl.Contents.Length);
            Array.Copy(rootClose, 0, rootedContents, rootOpen.Length + bl.Contents.Length, rootClose.Length);

            // Start an XmlReader over the contents:
            using (MemoryStream ms = new MemoryStream(rootedContents))
            using (StreamReader sr = new StreamReader(ms, Encoding.UTF8))
            using (XmlReader xr = XmlReader.Create(sr))
            {
                // Skip the fake root opening element:
                xr.ReadStartElement(rootElementName);

                // Begin reading document elements:
                do
                {
                    // Skip the closing EndElement for the fake root:
                    if (xr.Depth == 0 || xr.EOF)
                        break;

                    switch (xr.NodeType)
                    {
                        case XmlNodeType.Element:
                            switch (xr.LocalName)
                            {
                                case "schedule":
                                    processScheduleElement(xr, sb);
                                    break;
                                case "audience":
                                    processAudienceElement(xr, sb);
                                    break;
                                case "import":
                                    processImportElement(xr, sb);
                                    break;

                                // Normal XHTML node, just add its child nodes for processing:
                                default:
                                    sb.AppendFormat("<{0}", xr.LocalName);

                                    if (xr.HasAttributes && xr.MoveToFirstAttribute())
                                        do
                                        {
                                            string localName = xr.LocalName;
                                            char quoteChar = xr.QuoteChar;
                                            sb.AppendFormat(" {0}={1}", localName, quoteChar);
                                            // TODO: verify this is correct
                                            while (xr.ReadAttributeValue())
                                            {
                                                string content = xr.ReadContentAsString();
                                                string attrEncoded = System.Web.HttpUtility.HtmlAttributeEncode(content);
                                                sb.Append(attrEncoded);
                                            }
                                            sb.Append(quoteChar);
                                        } while (xr.MoveToNextAttribute());

                                    if (xr.IsEmptyElement)
                                        sb.Append(" />");
                                    else
                                        sb.Append(">");
                                    break;
                            }

                            break;
                        case XmlNodeType.EndElement:
                            sb.AppendFormat("</{0}>", xr.LocalName);
                            break;

                        case XmlNodeType.Whitespace:
                            // NOTE: Whitespace strips out '\r' chars apparently.
                            sb.Append(xr.Value);
                            break;

                        case XmlNodeType.Text:
                            // HTML-encode the text:
                            sb.Append(HttpUtility.HtmlEncode(xr.Value));
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                } while (xr.Read());
            }

            string result = sb.ToString();
            return new HTMLFragment(result);
        }

        private void processImportElement(XmlReader xr, StringBuilder sb)
        {
            throw new NotImplementedException();
        }

        private void processAudienceElement(XmlReader xr, StringBuilder sb)
        {
            throw new NotImplementedException();
        }

        private void processScheduleElement(XmlReader xr, StringBuilder sb)
        {
            throw new NotImplementedException();
        }
    }
}
