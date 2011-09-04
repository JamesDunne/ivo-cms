using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;

namespace IVO.CMS.Providers
{
    public sealed class RenderState
    {
        private XmlTextReader xr;
        public XmlTextReader Reader { get { return xr; } }

        private StringBuilder sb;
        public StringBuilder Writer { get { return sb; } }

        private BlobTreePath item;
        public BlobTreePath Item { get { return item; } }

        private ContentEngine engine;
        public ContentEngine Engine { get { return engine; } }

        private RenderState previous;
        public RenderState Previous { get { return previous; } }

        public RenderState(RenderState copy)
        {
            this.engine = copy.engine;

            this.item = copy.item;
            this.xr = copy.xr;
            this.sb = copy.sb;
            this.previous = copy;
        }

        public RenderState(ContentEngine engine)
        {
            this.engine = engine;

            this.item = null;
            this.xr = null;
            this.sb = null;
            this.previous = null;
        }

        public void Render(BlobTreePath item)
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
            this.item = item;
            sb = new StringBuilder(item.Blob.Contents.Length);

            // Start an XmlReader over the contents:
            using (MemoryStream ms = new MemoryStream(item.Blob.Contents))
            using (xr = new XmlTextReader(ms, XmlNodeType.Element, new XmlParserContext(null, null, null, XmlSpace.Default)))
            {
                // Start reading the document:
                xr.Read();

                StreamContent(DefaultProcessElements, () => false);
            }
        }

        /// <summary>
        /// Streaming copy from XmlTextReader and writing out to StringBuilder with event hooks for custom processing.
        /// </summary>
        /// <param name="xr"></param>
        /// <param name="sb"></param>
        /// <param name="action"></param>
        /// <param name="exit"></param>
        public void StreamContent(Func<bool> processElements, Func<bool> earlyExit)
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            do
            {
                if (earlyExit()) break;
                if (!processElements()) continue;

                switch (xr.NodeType)
                {
                    case XmlNodeType.Element:
                        // Normal XHTML node, start adding contents:
                        sb.AppendFormat("<{0}", xr.LocalName);

                        if (xr.HasAttributes && xr.MoveToFirstAttribute())
                            do
                            {
                                string localName = xr.LocalName;
                                char quoteChar = xr.QuoteChar;

                                sb.AppendFormat(" {0}={1}", localName, quoteChar);

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

        public bool DefaultProcessElements()
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            if (xr.NodeType == XmlNodeType.Element && xr.LocalName.StartsWith("cms-"))
            {
                ProcessCMSInstruction(xr.LocalName, this);

                // Skip normal copying behavior for this element:
                return false;
            }

            return true;
        }

        public static bool ProcessCMSInstruction(string elementName, RenderState state)
        {
            int knownDepth = state.xr.Depth;

            // Run the cms- element name through the custom-element provider chain:
            ICustomElementProvider provider = state.engine.CustomElementProviderRoot;
            
            bool processed = false;
            while (provider != null && !(processed = provider.ProcessCustomElement(elementName, state)))
            {
                provider = provider.Next;
            }

            if (!processed)
            {
                // Unrecognized 'cms-' element name, skip its contents entirely:
                state.SkipElementAndChildren(elementName);
                return false;
            }

            return true;
        }

        public void Error(string message)
        {
            var err = new SemanticError(message, item, xr.LineNumber, xr.LinePosition);
            engine.ReportError(err);

            // Inject an HTML comment describing the error:
            if (engine.InjectErrorComments)
                sb.AppendFormat("<!-- IVOCMS error in '{0}' ({1}:{2}): {3} -->", err.Item.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        #region Public utility methods

        public void SkipElementAndChildren(string elementName)
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            if (xr.NodeType != XmlNodeType.Element) Error(String.Format("expected start <{0}> element", elementName));
            if (xr.LocalName != elementName) Error(String.Format("expected start <{0}> element", elementName));
            if (xr.IsEmptyElement)
                return;

            int knownDepth = xr.Depth;

            // Read until we get back to the current depth:
            while (xr.Read() && xr.Depth > knownDepth) { }

            if (xr.NodeType != XmlNodeType.EndElement) Error(String.Format("expected end </{0}> element", elementName));
            if (xr.LocalName != elementName) Error(String.Format("expected end </{0}> element", elementName));

            //xr.ReadEndElement(/* elementName */);
        }

        public void CopyElementChildren(string elementName)
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            if (xr.NodeType != XmlNodeType.Element) Error(String.Format("expected start <{0}> element", elementName));
            if (xr.LocalName != elementName) Error(String.Format("expected start <{0}> element", elementName));
            // Nothing to do:
            if (xr.IsEmptyElement)
                return;

            int knownDepth = xr.Depth;
            // Shouldn't return false:
            if (!xr.Read()) Error("could not read content after <content> start element");

            // Stream-copy and process inner custom cms- elements until we get back to the current depth:
            new RenderState(this).StreamContent(DefaultProcessElements, () => xr.Depth == knownDepth);

            if (xr.NodeType != XmlNodeType.EndElement) Error(String.Format("expected end </{0}> element", elementName));
            if (xr.LocalName != elementName) Error(String.Format("expected end </{0}> element", elementName));

            //xr.ReadEndElement(/* elementName */);
        }

        #endregion
    }
}
