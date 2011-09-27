using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using IVO.Definition.Errors;

namespace IVO.CMS.Providers
{
    public sealed class RenderState
    {
        private XmlTextReader readFrom;
        /// <summary>
        /// Gets the current `XmlTextReader` used to parse the incoming XML blob.
        /// </summary>
        public XmlTextReader Reader { get { return readFrom; } }

        private StringBuilder writeTo;
        /// <summary>
        /// Gets the current `StringBuilder` used to output HTML5 polyglot document fragment code.
        /// </summary>
        public StringBuilder Writer { get { return writeTo; } }

        private Stack<StringBuilder> _writerStack = new Stack<StringBuilder>();

        /// <summary>
        /// Pushes off the current Writer to the stack and creates a new Writer based on the current Writer.
        /// </summary>
        public void PushWriter()
        {
            _writerStack.Push(writeTo);

            // Create a new StringBuilder from the current one so we can roll-back on error later:
            StringBuilder newSb = new StringBuilder(writeTo.ToString());
            writeTo = newSb;
        }

        /// <summary>
        /// Rolls back the current Writer to the last saved one.
        /// </summary>
        public void RollbackWriter()
        {
            writeTo = _writerStack.Pop();
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
        private Func<RenderState, bool> earlyExit;
        private Func<RenderState, Task<bool>> processElements;
        /// <summary>
        /// Gets the previous `RenderState`, i.e. the parser/writer state of the parent blob, if this
        /// blob is being rendered as an import.
        /// </summary>
        public RenderState Previous { get { return previous; } }

        public RenderState(RenderState copy, TreePathStreamedBlob item = null)
        {
            this.engine = copy.engine;

            this.item = item ?? copy.item;
            this.readFrom = copy.readFrom;
            this.writeTo = copy.writeTo;
            this.earlyExit = copy.earlyExit;
            this.processElements = copy.processElements;

            this.previous = copy;
        }

        public RenderState(ContentEngine engine, TreePathStreamedBlob item, XmlTextReader readFrom = null, StringBuilder writeTo = null, Func<RenderState, bool> earlyExit = null, Func<RenderState, Task<bool>> processElements = null, RenderState previous = null)
        {
            this.engine = engine;

            this.item = item;
            this.readFrom = readFrom;
            this.writeTo = writeTo;
            this.earlyExit = earlyExit ?? DefaultEarlyExit;
            this.processElements = processElements ?? DefaultProcessElements;

            this.previous = previous;
        }

        public async Task<Errorable<StringBuilder>> Render()
        {
            // Begin to stream contents from the blob:
            var err = await
                item.StreamedBlob.ReadStreamAsync(async sr =>
                {
                    // Create a string builder used to build the output polyglot HTML5 document fragment:
                    this.writeTo = writeTo ?? new StringBuilder((int)sr.Length);

                    // Start an XmlReader over the contents:
                    using (this.readFrom = new XmlTextReader(sr, XmlNodeType.Element, new XmlParserContext(null, null, null, XmlSpace.Default)))
                    {
                        // Start reading the document:
                        this.readFrom.Read();

                        // Stream in the content and output it to the StringBuilder:
                        await
                            this.StreamContent()
                            .ConfigureAwait(continueOnCapturedContext: false);
                    }

                    return Errorable.NoErrors;
                })
                .ConfigureAwait(continueOnCapturedContext: false);

            if (err.HasErrors) return err.Errors;

            return this.writeTo;
        }

        /// <summary>
        /// Streaming copy from XmlTextReader and writing out to StringBuilder with event hooks for custom processing.
        /// </summary>
        /// <param name="xr"></param>
        /// <param name="sb"></param>
        /// <param name="action"></param>
        /// <param name="exit"></param>
        public async Task StreamContent(Func<RenderState, bool> earlyExit = null, Func<RenderState, Task<bool>> processElements = null)
        {
            if (readFrom == null) throw new InvalidOperationException();
            if (writeTo == null) throw new InvalidOperationException();

            do
            {
                if ((earlyExit ?? this.earlyExit)(this)) break;
                if (!await (processElements ?? this.processElements)(this).ConfigureAwait(continueOnCapturedContext: false)) continue;

                switch (readFrom.NodeType)
                {
                    case XmlNodeType.Element:
                        // Normal XHTML node, start adding contents:
                        writeTo.AppendFormat("<{0}", readFrom.LocalName);

                        // Output attributes:
                        if (readFrom.HasAttributes && readFrom.MoveToFirstAttribute())
                        {
                            do
                            {
                                string localName = readFrom.LocalName;
                                char quoteChar = readFrom.QuoteChar;

                                writeTo.AppendFormat(" {0}={1}", localName, quoteChar);

                                while (readFrom.ReadAttributeValue())
                                {
                                    string content = readFrom.ReadContentAsString();
                                    string attrEncoded = System.Web.HttpUtility.HtmlAttributeEncode(content);
                                    writeTo.Append(attrEncoded);
                                }

                                writeTo.Append(quoteChar);
                            } while (readFrom.MoveToNextAttribute());

                            readFrom.MoveToElement();
                        }

                        // Close the element:
                        if (readFrom.IsEmptyElement)
                            writeTo.Append(" />");
                        else
                            writeTo.Append(">");
                        break;

                    case XmlNodeType.EndElement:
                        writeTo.AppendFormat("</{0}>", readFrom.LocalName);
                        break;

                    case XmlNodeType.Whitespace:
                        writeTo.Append(readFrom.Value);
                        break;

                    case XmlNodeType.Text:
                        // HTML-encode the text:
                        writeTo.Append(HttpUtility.HtmlEncode(readFrom.Value));
                        break;

                    case XmlNodeType.EntityReference:
                        // HTML-encode the entity reference:
                        writeTo.Append(HttpUtility.HtmlEncode(readFrom.Value));
                        break;

                    case XmlNodeType.Comment:
                        // FIXME: encode the comment text somehow? What rules?
                        writeTo.AppendFormat("<!--{0}-->", readFrom.Value);
                        break;

                    case XmlNodeType.CDATA:
                        // No specific reason, just don't feel like dealing with it.
                        throw new NotSupportedException("CDATA is not supported by this CMS.");

                    case XmlNodeType.XmlDeclaration:
                        break;

                    default:
                        // Whatever else is unnecessary:
                        throw new NotImplementedException(String.Format("Node type {0} not implemented!", readFrom.NodeType));
                }
            } while (readFrom.Read());
        }

        public static async Task<bool> DefaultProcessElements(RenderState st)
        {
            if (st.Reader == null) throw new InvalidOperationException();
            if (st.Writer == null) throw new InvalidOperationException();

            if (st.Reader.NodeType == XmlNodeType.Element && st.Reader.LocalName.StartsWith("cms-"))
            {
                // Call out to the custom element handlers:
                await ProcessCMSInstruction(st.Reader.LocalName, st).ConfigureAwait(continueOnCapturedContext: false);

                // Skip normal copying behavior for this element:
                return false;
            }

            return true;
        }

        public static bool DefaultEarlyExit(RenderState st)
        {
            return false;
        }

        public static async Task<bool> ProcessCMSInstruction(string elementName, RenderState state)
        {
            string openingElement = state.readFrom.LocalName;
            int openingDepth = state.readFrom.Depth;
            bool openingEmpty = state.readFrom.IsEmptyElement;

            // Run the cms- element name through the custom-element provider chain:
            ICustomElementProvider provider = state.engine.CustomElementProviderRoot;
            
            // Run down the chain until a provider picks up the element and processes its contents:
            bool processed = false;
            while (provider != null)
            {
                if (true == (processed = await provider.ProcessCustomElement(elementName, state).ConfigureAwait(continueOnCapturedContext: false)))
                    break;

                provider = provider.Next;
            }

            if (!processed)
            {
                // Unrecognized 'cms-' element name, skip its contents entirely.

                // Validate that the XmlTextReader is at the same state before the processor chain was invoked:
                // This is to detect bad custom element processors that attempt to affect state when they report
                // they have not.

                // We must be at the same element of the custom instruction:
                if (state.readFrom.NodeType != XmlNodeType.Element)
                    state.Error("custom element provider left XML parser on an unexpected node type");
                // The element name must be the same:
                if (state.readFrom.LocalName != openingElement)
                    state.Error("custom element provider left XML parser on an unexpected end element");
                // The depth level must be the same:
                if (state.readFrom.Depth != openingDepth)
                    state.Error("custom element provider left XML parser at an unexpected depth level");

                // Issue a warning:
                state.WarningSuppressComment("No custom element providers processed unknown element, '{0}'; skipping its contents entirely.", openingElement);
                state.SkipElementAndChildren(elementName);
                
                return false;
            }

            // We must be at the end (or same, if empty) element of the custom instruction:
            if (state.readFrom.NodeType != (openingEmpty ? XmlNodeType.Element : XmlNodeType.EndElement))
                state.Error("custom element provider left XML parser on an unexpected node type");
            // The element name must be the same:
            if (state.readFrom.LocalName != openingElement)
                state.Error("custom element provider left XML parser on an unexpected end element");
            // The depth level must be the same:
            if (state.readFrom.Depth != openingDepth)
                state.Error("custom element provider left XML parser at an unexpected depth level");

            return true;
        }

        #region Warning and Error reporting

        public void Warning(string message)
        {
            var warn = new SemanticWarning(message, item, readFrom.LineNumber, readFrom.LinePosition);
            engine.ReportWarning(warn);

            if (engine.InjectWarningComments)
                writeTo.AppendFormat("<!-- IVOCMS warning in '{0}' ({1}:{2}): {3} -->", warn.Item.TreeBlobPath.Path, warn.LineNumber, warn.LinePosition, warn.Message);
        }

        public void Warning(string format, params object[] args)
        {
            Warning(String.Format(format, args));
        }

        public void WarningSuppressComment(string message)
        {
            var warn = new SemanticWarning(message, item, readFrom.LineNumber, readFrom.LinePosition);
            engine.ReportWarning(warn);
        }

        public void WarningSuppressComment(string format, params object[] args)
        {
            WarningSuppressComment(String.Format(format, args));
        }

        public void Error(string message)
        {
            var err = new SemanticError(message, item, readFrom.LineNumber, readFrom.LinePosition);
            engine.ReportError(err);

            // Inject an HTML comment describing the error:
            if (engine.InjectErrorComments)
                writeTo.AppendFormat("<!-- IVOCMS error in '{0}' ({1}:{2}): {3} -->", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        public void Error(string format, params object[] args)
        {
            var err = new SemanticError(String.Format(format, args), item, readFrom.LineNumber, readFrom.LinePosition);
            engine.ReportError(err);

            // Inject an HTML comment describing the error:
            if (engine.InjectErrorComments)
                writeTo.AppendFormat("<!-- IVOCMS error in '{0}' ({1}:{2}): {3} -->", err.Item.TreeBlobPath.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        public void ErrorSuppressComment(string message)
        {
            var err = new SemanticError(message, item, readFrom.LineNumber, readFrom.LinePosition);
            engine.ReportError(err);
        }

        public void ErrorSuppressComment(string format, params object[] args)
        {
            var err = new SemanticError(String.Format(format, args), item, readFrom.LineNumber, readFrom.LinePosition);
            engine.ReportError(err);
        }

        #endregion

        #region Public utility methods

        public void SkipElementAndChildren(string elementName)
        {
            if (readFrom == null) throw new InvalidOperationException();
            if (writeTo == null) throw new InvalidOperationException();

            if (readFrom.NodeType != XmlNodeType.Element) Error("expected start <{0}> element", elementName);
            if (readFrom.LocalName != elementName) Error("expected start <{0}> element", elementName);
            if (readFrom.IsEmptyElement)
                return;

            // Read until we get back to the current depth:
            int knownDepth = readFrom.Depth;
            while (readFrom.Read() && readFrom.Depth > knownDepth) { }

            if (readFrom.NodeType != XmlNodeType.EndElement) Error("expected end </{0}> element", elementName);
            if (readFrom.LocalName != elementName) Error("expected end </{0}> element", elementName);
        }

        public async Task CopyElementChildren(string elementName, Func<RenderState, bool> earlyExit = null, Func<RenderState, Task<bool>> processElements = null)
        {
            if (readFrom == null) throw new InvalidOperationException();
            if (writeTo == null) throw new InvalidOperationException();

            if (readFrom.NodeType != XmlNodeType.Element) Error("expected start <{0}> element", elementName);
            if (readFrom.LocalName != elementName) Error("expected start <{0}> element", elementName);
            // Nothing to do:
            if (readFrom.IsEmptyElement)
                return;

            int knownDepth = readFrom.Depth;

            // Shouldn't return false:
            if (!readFrom.Read()) Error("could not read content after <content> start element");

            // Stream-copy and process inner custom cms- elements until we get back to the current depth:
            await new RenderState(this)
                .StreamContent(
                    earlyExit ?? (st => st.Reader.Depth == knownDepth),
                    processElements ?? DefaultProcessElements
                ).ConfigureAwait(continueOnCapturedContext: false);

            if (readFrom.NodeType != XmlNodeType.EndElement) Error("expected end </{0}> element", elementName);
            if (readFrom.LocalName != elementName) Error("expected end </{0}> element", elementName);
        }

        #endregion
    }
}
