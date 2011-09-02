using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using IVO.Definition.Models;
using IVO.Definition.Repositories;

namespace IVO.CMS
{
    public sealed class ContentEngine
    {
        private const string rootElementName = "_root_";

        private static readonly byte[] rootOpen = Encoding.UTF8.GetBytes("<" + rootElementName + ">");
        private static readonly byte[] rootClose = Encoding.UTF8.GetBytes("</" + rootElementName +">");

        private ITreeRepository trrepo;
        private IBlobRepository blrepo;

        public ContentEngine(ITreeRepository trrepo, IBlobRepository blrepo)
        {
            this.trrepo = trrepo;
            this.blrepo = blrepo;
        }

        public HTMLFragment RenderBlob(CanonicalizedAbsolutePath path, TreeID rootid, Blob bl)
        {
            // NOTE: I would much prefer to load in a Stream from the persistence store rather than a `byte[]`.
            // It seems the only way to do this from a SqlDataReader is with its GetBytes() method. Furthermore,
            // this would require the blob query to be executed with the CommandBehavior.SequentialAccess and
            // the data would have to be streamed in while the SqlDataReader is open and passed to this method,
            // which would be quite a tightly-integrated model. Perhaps the query class could be passed a lambda
            // which then gets invoked during SqlDataReader parsing. That would call into this method so we could
            // operate over blobs streamed directly from the database. Creating a new StreamedBlob type would be
            // a good fit here too.
            // Side note: The SqlDataReader's Stream abstractions are broken and only operate over in-memory
            // data copies, thus defeating the purpose of streaming from the persistence store.

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
                            if (xr.LocalName.StartsWith("cms-"))
                            {
                                processCMSInstruction(xr.LocalName, xr, sb);
                                break;
                            }

                            // Normal XHTML node, start adding contents:
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
                        case XmlNodeType.EndElement:
                            sb.AppendFormat("</{0}>", xr.LocalName);
                            break;

                        case XmlNodeType.Whitespace:
                            // NOTE: Whitespace node strips out '\r' chars apparently.
                            sb.Append(xr.Value);
                            break;

                        case XmlNodeType.Text:
                            // HTML-encode the text:
                            sb.Append(HttpUtility.HtmlEncode(xr.Value));
                            break;

                        case XmlNodeType.EntityReference:
                            sb.Append(HttpUtility.HtmlEncode(xr.Value));
                            break;

                        case XmlNodeType.Comment:
                            // FIXME: encode the comment text somehow? What rules?
                            sb.AppendFormat("<!--{0}-->", xr.Value);
                            break;

                        case XmlNodeType.CDATA:
                            throw new NotSupportedException("CDATA is not supported by this CMS.");

                        default:
                            throw new NotImplementedException();
                    }
                } while (xr.Read());
            }

            // Clear the reference to make GC's job a bit easier:
            rootedContents = null;

            string result = sb.ToString();
            return new HTMLFragment(result);
        }

        private void skipElementAndChildren(string elementName, XmlReader xr)
        {
            if (xr.NodeType != XmlNodeType.Element) throw new InvalidOperationException();
            if (xr.LocalName != elementName) throw new InvalidOperationException();
            if (xr.IsEmptyElement)
            {
                return;
            }
            
            int knownDepth = xr.Depth;

            // Read until we get back to the current depth:
            while (xr.Read() && xr.Depth > knownDepth) { }

            if (xr.NodeType != XmlNodeType.EndElement) throw new InvalidOperationException();
            if (xr.LocalName != elementName) throw new InvalidOperationException();

            //xr.ReadEndElement(/* elementName */);
        }

        private void processCMSInstruction(string elementName, XmlReader xr, StringBuilder sb)
        {
            int knownDepth = xr.Depth;

            // Skip the 'cms-' prefix and delegate to the instruction handlers:
            switch (elementName.Substring(4))
            {
                case "import": processImportElement(xr, sb); break;
                case "targeted": processTargetedElement(xr, sb); break;
                case "scheduled": processScheduledElement(xr, sb); break;
                default:
                    // Unrecognized 'cms-' element name, skip it entirely:
                    if (xr.IsEmptyElement) break;

                    // Read until we get back to our current depth level:
                    while (xr.Read() && xr.Depth > knownDepth) {}
                    
                    // Ensure that the 'cms-' element is closed:
                    if (xr.NodeType != XmlNodeType.EndElement) throw new InvalidOperationException();
                    if (xr.LocalName != elementName) throw new InvalidOperationException();
                    
                    //xr.ReadEndElement(/* elementName */);
                    break;
            }
        }

        private void processImportElement(XmlReader xr, StringBuilder sb)
        {
            // Imports content directly from another blob, addressable by a relative path or an absolute path.
            // Relative path is always relative to the current blob's absolute path.
            // In the case of nested imports, relative paths are relative to the absolute path of the importee's parent blob.

            // <import relative-path="../templates/main" />
            // <import absolute-path="/templates/main" />

            // Absolute paths are canonicalized. An exception will be thrown if the path contains too many '..' references that
            // bring the canonicalized path above the root of the tree (which is impossible).

            // Recursively call RenderBlob on the imported blob and include the rendered HTMLFragment into this rendering.
            skipElementAndChildren("cms-import", xr);
        }

        private void processTargetedElement(XmlReader xr, StringBuilder sb)
        {
            // <targeted>
            //   <!--
            //     Order matters. Most specific targets come first; least specific targets go last.
            //     Target attributes are user-defined. They must be valid XML attributes.
            //     The custom attributes are collected into a Dictionary<string, string> and passed to
            //     the "target evaluation provider" to evaluate if the target attributes indicate that
            //     the content applies to the current user viewing the content.
            //   -->
            //   <target userType="Employee" department="Sales">
            //     ... employee-targeted content here, specifically for Sales department ...
            //   </target>
            //   <target userType="Manager" />
            //     ... manager-targeted content here, not specific to a department ...
            //   <target userType="Employee" />
            //   <else>
            //     ... default content displayed if the above targets do not match ...
            //   </else>
            // </targeted>
            skipElementAndChildren("cms-targeted", xr);
        }

        private void processScheduledElement(XmlReader xr, StringBuilder sb)
        {
            // Specifies that content should be scheduled for the entire month of August
            // and the entire month of October but NOT the month of September.
            // 'from' is inclusive date/time.
            // 'to'   is exclusive date/time.
            // <scheduled>
            //   <date-range from="2011-08-01 00:00 -0500" to="2011-09-01 00:00 -0500" />
            //   <date-range from="2011-10-01 00:00 -0500" to="2011-11-01 00:00 -0500" />
            //   <content>
            //     ...
            //   </content>
            // </scheduled>
            skipElementAndChildren("cms-scheduled", xr);
        }
    }
}
