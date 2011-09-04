using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Web;
using IVO.Definition.Models;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;
using IVO.Definition.Repositories;

namespace IVO.CMS.Providers
{
    public sealed class RenderState
    {
        private XmlTextReader xr;
        private StringBuilder sb;

        public XmlTextReader Reader { get { return xr; } }
        public StringBuilder Writer { get { return sb; } }

        private BlobTreePath item;
        public BlobTreePath Item { get { return item; } }

        private ContentEngine engine;

        public RenderState Previous { get; private set; }

        public RenderState(RenderState copy)
        {
            this.engine = copy.engine;

            this.item = copy.item;
            this.xr = copy.xr;
            this.sb = copy.sb;
            this.Previous = copy;
        }

        public RenderState(ContentEngine engine)
        {
            this.engine = engine;

            this.item = null;
            this.xr = null;
            this.sb = null;
            this.Previous = null;
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
            if (xr.NodeType == XmlNodeType.Element && xr.LocalName.StartsWith("cms-"))
            {
                ProcessCMSInstruction(xr.LocalName, this);

                // Skip normal copying behavior for this element:
                return false;
            }

            return true;
        }

        #region Private implementation details

        private void error(string message)
        {
            var err = new SemanticError(message, item, xr.LineNumber, xr.LinePosition);
            engine.ReportError(err);

            // Inject an HTML comment describing the error:
            if (engine.InjectErrorComments)
                sb.AppendFormat("<!-- IVOCMS error in '{0}' ({1}:{2}): {3} -->", err.Item.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        private void skipElementAndChildren(string elementName)
        {
            if (xr.NodeType != XmlNodeType.Element) error(String.Format("expected start <{0}> element", elementName));
            if (xr.LocalName != elementName) error(String.Format("expected start <{0}> element", elementName));
            if (xr.IsEmptyElement)
                return;

            int knownDepth = xr.Depth;

            // Read until we get back to the current depth:
            while (xr.Read() && xr.Depth > knownDepth) { }

            if (xr.NodeType != XmlNodeType.EndElement) error(String.Format("expected end </{0}> element", elementName));
            if (xr.LocalName != elementName) error(String.Format("expected end </{0}> element", elementName));

            //xr.ReadEndElement(/* elementName */);
        }

        private void streamElementChildren(string elementName)
        {
            if (xr.NodeType != XmlNodeType.Element) error(String.Format("expected start <{0}> element", elementName));
            if (xr.LocalName != elementName) error(String.Format("expected start <{0}> element", elementName));
            // Nothing to do:
            if (xr.IsEmptyElement)
                return;

            int knownDepth = xr.Depth;
            // Shouldn't return false:
            if (!xr.Read()) error("could not read content after <content> start element");

            // Stream-copy and process inner custom cms- elements until we get back to the current depth:
            new RenderState(this).StreamContent(DefaultProcessElements, () => xr.Depth == knownDepth);

            if (xr.NodeType != XmlNodeType.EndElement) error(String.Format("expected end </{0}> element", elementName));
            if (xr.LocalName != elementName) error(String.Format("expected end </{0}> element", elementName));

            //xr.ReadEndElement(/* elementName */);
        }

        public static bool ProcessCMSInstruction(string elementName, RenderState state)
        {
            int knownDepth = state.xr.Depth;

            // Skip the 'cms-' prefix and delegate to the instruction handlers:
            switch (elementName.Substring(4))
            {
                case "import": state.processImportElement(); break;
                case "targeted": state.processTargetedElement(); break;
                case "scheduled": state.processScheduledElement(); break;
                case "list": throw new NotImplementedException();
                default:
                    // Not a built-in 'cms-' element name, check the custom element provider:
                    ICustomElementProvider provider = null;
                    bool processed = false;
                    while (provider != null && !(processed = provider.ProcessCustomElement(elementName, state)))
                    {
                        provider = provider.Next;
                    }

                    if (!processed)
                    {
                        // Unrecognized 'cms-' element name, skip it entirely:
                        state.skipElementAndChildren(elementName);
                        return false;
                    }

                    return true;
            }

            return true;
        }

        #endregion

        #region Default cms- element processing

        private void processImportElement()
        {
            // Imports content directly from another blob, addressable by a relative path or an absolute path.
            // Relative path is always relative to the current blob's absolute path.
            // In the case of nested imports, relative paths are relative to the absolute path of the importee's parent blob.

            // <cms-import relative-path="../templates/main" />
            // <cms-import absolute-path="/templates/main" />

            // Absolute paths are canonicalized. An exception will be thrown if the path contains too many '..' references that
            // bring the canonicalized path above the root of the tree (which is impossible).

            // Recursively call RenderBlob on the imported blob and include the rendered HTMLFragment into this rendering.

            // xr is pointing to "cms-import" Element.
            if (!xr.IsEmptyElement) error("cms-import element must be empty");
            
            if (xr.HasAttributes && xr.MoveToFirstAttribute())
            {
                string relPath = xr.GetAttribute("relative-path");
                string absPath = xr.GetAttribute("absolute-path");

                // Check mutual exclusion of attributes:
                if ((absPath == null) == (relPath == null)) error("cms-import must have either 'relative-path' or 'absolute-path' attribute but not both");
                
                // Canonicalize the path:
                CanonicalBlobPath path;
                if (absPath != null)
                {
                    path = ((AbsoluteBlobPath)absPath).Canonicalize();
                }
                else
                {
                    // Apply the relative path to the current item's absolute path:
                    RelativeBlobPath rbp = (RelativeBlobPath)relPath;
                    CanonicalTreePath ctp = (Item.Path.Tree + rbp.Tree).Canonicalize();
                    path = new CanonicalBlobPath(ctp, rbp.Name);
                }

                // Fetch the Blob given the absolute path constructed:
                Task<BlobTreePath> tBlob = engine.Blobs.GetBlobByAbsolutePath(Item.RootTreeID, path);
                
                // TODO: we could probably asynchronously load blobs and render their contents
                // then at a final sync point go in and inject their contents into the proper
                // places in each imported blob's parent StringBuilder.
                tBlob.Wait();

                // No blob? Put up an error:
                if (tBlob.Result == null)
                {
                    error(String.Format("path '{0}' not found", path));
                    return;
                }
                
                // Render the blob inline:
                RenderState rsInner = new RenderState(this);
                rsInner.Render(tBlob.Result);
                string innerResult = rsInner.Writer.ToString();
                sb.Append(innerResult);
            }
        }

        private void processTargetedElement()
        {
            // Order matters. Most specific targets come first; least specific targets go last.
            // Target attributes are user-defined. They must be valid XML attributes.
            // The custom attributes are collected into a Dictionary<string, string> and passed to
            // the "target evaluation provider" to evaluate if the target attributes indicate that
            // the content applies to the current user viewing the content.
            // <cms-targeted>
            //   <if userType="Employee" department="Sales">
            //     ... employee-targeted content here, specifically for Sales department ...
            //   </if>
            //   <if userType="Manager">
            //     ... manager-targeted content here, not specific to a department ...
            //   </if>
            //   <if userType="Employee">
            //     ... employee-targeted content here, not specific to a department ...
            //   </if>
            //   <else>
            //     ... default content displayed if the above targets do not match ...
            //   </else>
            // </cms-targeted>

            skipElementAndChildren("cms-targeted");
        }

        private void processScheduledElement()
        {
            // Specifies that content should be scheduled for the entire month of August
            // and the entire month of October but NOT the month of September.
            // 'from' is inclusive date/time.
            // 'to'   is exclusive date/time.
            // <cms-scheduled>
            //   <range from="2011-08-01 00:00 -0500" to="2011-09-01 00:00 -0500" />
            //   <range from="2011-10-01 00:00 -0500" to="2011-11-01 00:00 -0500" />
            //   <content>
            //     content to show if scheduled (recursively including other cms- elements)
            //   </content>
            // [optional:]
            //   <else>
            //     content to show if not scheduled (recursively including other cms- elements)
            //   </else>
            // </cms-scheduled>
            
            bool displayContent = false;
            bool hasRanges = false;
            bool hasContent = false;
            bool hasElse = false;

            int knownDepth = xr.Depth;
            while (xr.Read() && xr.Depth > knownDepth)
            {
                if (xr.NodeType != XmlNodeType.Element) continue;

                if (xr.LocalName == "range")
                {
                    hasRanges = true;

                    if (!xr.IsEmptyElement)
                    {
                        error("range element must be empty");
                        // TODO: skip to end of cms-scheduled element and exit.
                        continue;
                    }

                    // If we're already good to display, don't bother evaluating further schedule ranges:
                    if (displayContent)
                        // Safe to continue here because the element is empty; no more to parse.
                        continue;

                    string fromAttr, toAttr;

                    // Validate the element's form:
                    if (!xr.HasAttributes) error("range element must have attributes");
                    if ((fromAttr = xr.GetAttribute("from")) == null) error("range element must have 'from' attribute");
                    // 'to' attribute is optional:
                    toAttr = xr.GetAttribute("to");

                    // Parse the dates:
                    DateTimeOffset fromDate, toDateTmp;
                    DateTimeOffset toDate = DateTimeOffset.Now;

                    if (!DateTimeOffset.TryParse(fromAttr, out fromDate)) error("could not parse 'from' attribute as a date/time");
                    if (!String.IsNullOrWhiteSpace(toAttr))
                    {
                        if (DateTimeOffset.TryParse(toAttr, out toDateTmp))
                            toDate = toDateTmp;
                        else
                            error("could not parse 'to' attribute as a date/time");
                    }

                    // Validate the range's dates are ordered correctly:
                    if (toDate <= fromDate) error("'to' date must be later than 'from' date or empty");

                    // Check the schedule range:
                    displayContent = (engine.ViewDate >= fromDate && engine.ViewDate < toDate);
                }
                else if (xr.LocalName == "content")
                {
                    if (hasContent) error("only one content element may exist in cms-scheduled");

                    hasContent = true;
                    if (!hasRanges) error("no range elements found before content element in cms-scheduled");

                    if (displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        streamElementChildren("content");
                        xr.ReadEndElement(/* "content" */);
                    }
                    else
                    {
                        // Skip the inner content entirely:
                        skipElementAndChildren("content");
                        xr.ReadEndElement(/* "content" */);
                    }
                }
                else if (xr.LocalName == "else")
                {
                    if (hasElse) error("only one else element may exist in cms-scheduled");
                    hasElse = true;

                    if (!displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        streamElementChildren("else");
                        xr.ReadEndElement(/* "else" */);
                    }
                    else
                    {
                        // Skip the inner content entirely:
                        skipElementAndChildren("else");
                        xr.ReadEndElement(/* "else" */);
                    }
                }
                else
                {
                    error(String.Format("unexpected element '{0}'", xr.LocalName));
                }
            }

            // Report errors:
            if (!hasRanges) error("no range elements found");
            if (!hasContent) error("no content element found");

            // Skip Whitespace and Comments etc. until we find the end element:
            while (xr.NodeType != XmlNodeType.EndElement && xr.Read()) { }

            // Validate:
            if (xr.LocalName != "cms-scheduled") error("expected end <cms-scheduled/> element");

            // Don't read this element because the next `xr.Read()` in the main loop will:
            //xr.ReadEndElement(/* "cms-scheduled" */);
        }

        #endregion
    }
}
