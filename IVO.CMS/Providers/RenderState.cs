using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using System.Collections.Generic;

namespace IVO.CMS.Providers
{
    public sealed class RenderState
    {
        private XmlTextReader xr;
        /// <summary>
        /// Gets the current `XmlTextReader` used to parse the incoming XML blob.
        /// </summary>
        public XmlTextReader Reader { get { return xr; } }

        private StringBuilder sb;
        /// <summary>
        /// Gets the current `StringBuilder` used to output HTML5 polyglot document fragment code.
        /// </summary>
        public StringBuilder Writer { get { return sb; } }

        private Stack<StringBuilder> _writerStack = new Stack<StringBuilder>();

        /// <summary>
        /// Pushes off the current Writer to the stack and creates a new Writer based on the current Writer.
        /// </summary>
        public void PushWriter()
        {
            _writerStack.Push(sb);

            // Create a new StringBuilder from the current one so we can roll-back on error later:
            StringBuilder newSb = new StringBuilder(sb.ToString());
            sb = newSb;
        }

        /// <summary>
        /// Rolls back the current Writer to the last saved one.
        /// </summary>
        public void RollbackWriter()
        {
            sb = _writerStack.Pop();
        }

        /// <summary>
        /// Pops the last Writer off the stack and uses the current Writer.
        /// </summary>
        public void CommitWriter()
        {
            _writerStack.Pop();
        }

        private TreePathStreamedBlob item;
        /// <summary>
        /// Gets the current processed blob and its canonical absolute path from its root TreeID.
        /// </summary>
        public TreePathStreamedBlob Item { get { return item; } }

        private ContentEngine engine;
        /// <summary>
        /// Gets the current `ContentEngine` context.
        /// </summary>
        public ContentEngine Engine { get { return engine; } }

        private RenderState previous;
        /// <summary>
        /// Gets the previous `RenderState`, i.e. the parser/writer state of the parent blob, if this
        /// blob is being rendered as an import.
        /// </summary>
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

        public void Render(TreePathStreamedBlob item)
        {
            // Begin to stream contents from the blob:
            item.StreamedBlob.ReadStream(sr =>
            {
                // Create a string builder used to build the output polyglot HTML5 document fragment:
                this.item = item;
                this.sb = new StringBuilder((int)sr.Length);

                // Start an XmlReader over the contents:
                using (this.xr = new XmlTextReader(sr, XmlNodeType.Element, new XmlParserContext(null, null, null, XmlSpace.Default)))
                {
                    // Start reading the document:
                    this.xr.Read();

                    // Stream in the content and output it to the StringBuilder:
                    this.StreamContent(this.DefaultProcessElements, this.DefaultEarlyExit);
                }
            }).Wait();
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

                        // Output attributes:
                        if (xr.HasAttributes && xr.MoveToFirstAttribute())
                        {
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

                            xr.MoveToElement();
                        }

                        // Close the element:
                        if (xr.IsEmptyElement)
                            sb.Append(" />");
                        else
                            sb.Append(">");
                        break;

                    case XmlNodeType.EndElement:
                        sb.AppendFormat("</{0}>", xr.LocalName);
                        break;

                    case XmlNodeType.Whitespace:
                        sb.Append(xr.Value);
                        break;

                    case XmlNodeType.Text:
                        // HTML-encode the text:
                        sb.Append(HttpUtility.HtmlEncode(xr.Value));
                        break;

                    case XmlNodeType.EntityReference:
                        // HTML-encode the entity reference:
                        sb.Append(HttpUtility.HtmlEncode(xr.Value));
                        break;

                    case XmlNodeType.Comment:
                        // FIXME: encode the comment text somehow? What rules?
                        sb.AppendFormat("<!--{0}-->", xr.Value);
                        break;

                    case XmlNodeType.CDATA:
                        // No specific reason, just don't feel like dealing with it.
                        throw new NotSupportedException("CDATA is not supported by this CMS.");

                    case XmlNodeType.XmlDeclaration:
                        break;

                    default:
                        // Whatever else is unnecessary:
                        throw new NotImplementedException(String.Format("Node type {0} not implemented!", xr.NodeType));
                }
            } while (xr.Read());
        }

        public bool DefaultProcessElements()
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            if (xr.NodeType == XmlNodeType.Element && xr.LocalName.StartsWith("cms-"))
            {
                // Call out to the custom element handlers:
                ProcessCMSInstruction(xr.LocalName, this);

                // Skip normal copying behavior for this element:
                return false;
            }

            return true;
        }

        public bool DefaultEarlyExit()
        {
            return false;
        }

        public static bool ProcessCMSInstruction(string elementName, RenderState state)
        {
            string openingElement = state.xr.LocalName;
            int openingDepth = state.xr.Depth;
            bool openingEmpty = state.xr.IsEmptyElement;

            // Run the cms- element name through the custom-element provider chain:
            ICustomElementProvider provider = state.engine.CustomElementProviderRoot;
            
            // Run down the chain until a provider picks up the element and processes its contents:
            bool processed = false;
            while (provider != null && !(processed = provider.ProcessCustomElement(elementName, state)))
            {
                provider = provider.Next;
            }

            if (!processed)
            {
                // Unrecognized 'cms-' element name, skip its contents entirely.

                // Validate that the XmlTextReader is at the same state before the processor chain was invoked:
                // This is to detect bad custom element processors that attempt to affect state when they report
                // they have not.

                // We must be at the same element of the custom instruction:
                if (state.xr.NodeType != XmlNodeType.Element)
                    state.Error("custom element provider left XML parser on an unexpected node type");
                // The element name must be the same:
                if (state.xr.LocalName != openingElement)
                    state.Error("custom element provider left XML parser on an unexpected end element");
                // The depth level must be the same:
                if (state.xr.Depth != openingDepth)
                    state.Error("custom element provider left XML parser at an unexpected depth level");

                // Issue a warning:
                state.WarningSuppressComment("No custom element providers processed unknown element, '{0}'; skipping its contents entirely.", openingElement);
                state.SkipElementAndChildren(elementName);
                
                return false;
            }

            // We must be at the end (or same, if empty) element of the custom instruction:
            if (state.xr.NodeType != (openingEmpty ? XmlNodeType.Element : XmlNodeType.EndElement))
                state.Error("custom element provider left XML parser on an unexpected node type");
            // The element name must be the same:
            if (state.xr.LocalName != openingElement)
                state.Error("custom element provider left XML parser on an unexpected end element");
            // The depth level must be the same:
            if (state.xr.Depth != openingDepth)
                state.Error("custom element provider left XML parser at an unexpected depth level");

            return true;
        }

        public void Warning(string message)
        {
            var warn = new SemanticWarning(message, item, xr.LineNumber, xr.LinePosition);
            engine.ReportWarning(warn);

            if (engine.InjectWarningComments)
                sb.AppendFormat("<!-- IVOCMS warning in '{0}' ({1}:{2}): {3} -->", warn.Item.TreeBlobPath.Path, warn.LineNumber, warn.LinePosition, warn.Message);
        }

        public void Warning(string format, params object[] args)
        {
            Warning(String.Format(format, args));
        }

        public void WarningSuppressComment(string message)
        {
            var warn = new SemanticWarning(message, item, xr.LineNumber, xr.LinePosition);
            engine.ReportWarning(warn);
        }

        public void WarningSuppressComment(string format, params object[] args)
        {
            WarningSuppressComment(String.Format(format, args));
        }

        public void Error(string message)
        {
            var err = new SemanticError(message, item, xr.LineNumber, xr.LinePosition);
            engine.ReportError(err);

            // Inject an HTML comment describing the error:
            if (engine.InjectErrorComments)
                sb.AppendFormat("<!-- IVOCMS error in '{0}' ({1}:{2}): {3} -->", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        public void Error(string format, params object[] args)
        {
            var err = new SemanticError(String.Format(format, args), item, xr.LineNumber, xr.LinePosition);
            engine.ReportError(err);

            // Inject an HTML comment describing the error:
            if (engine.InjectErrorComments)
                sb.AppendFormat("<!-- IVOCMS error in '{0}' ({1}:{2}): {3} -->", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        public void ErrorSuppressComment(string message)
        {
            var err = new SemanticError(message, item, xr.LineNumber, xr.LinePosition);
            engine.ReportError(err);
        }

        public void ErrorSuppressComment(string format, params object[] args)
        {
            var err = new SemanticError(String.Format(format, args), item, xr.LineNumber, xr.LinePosition);
            engine.ReportError(err);
        }

        #region Public utility methods

        public void SkipElementAndChildren(string elementName)
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            if (xr.NodeType != XmlNodeType.Element) Error("expected start <{0}> element", elementName);
            if (xr.LocalName != elementName) Error("expected start <{0}> element", elementName);
            if (xr.IsEmptyElement)
                return;

            // Read until we get back to the current depth:
            int knownDepth = xr.Depth;
            while (xr.Read() && xr.Depth > knownDepth) { }

            if (xr.NodeType != XmlNodeType.EndElement) Error("expected end </{0}> element", elementName);
            if (xr.LocalName != elementName) Error("expected end </{0}> element", elementName);
        }

        public void CopyElementChildren(string elementName)
        {
            if (xr == null) throw new InvalidOperationException();
            if (sb == null) throw new InvalidOperationException();

            if (xr.NodeType != XmlNodeType.Element) Error("expected start <{0}> element", elementName);
            if (xr.LocalName != elementName) Error("expected start <{0}> element", elementName);
            // Nothing to do:
            if (xr.IsEmptyElement)
                return;

            int knownDepth = xr.Depth;

            // Shouldn't return false:
            if (!xr.Read()) Error("could not read content after <content> start element");

            // Stream-copy and process inner custom cms- elements until we get back to the current depth:
            new RenderState(this).StreamContent(DefaultProcessElements, () => xr.Depth == knownDepth);

            if (xr.NodeType != XmlNodeType.EndElement) Error("expected end </{0}> element", elementName);
            if (xr.LocalName != elementName) Error("expected end </{0}> element", elementName);
        }

        #endregion
    }
}
