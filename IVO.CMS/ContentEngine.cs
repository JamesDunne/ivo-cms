using System;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using IVO.Definition.Models;
using IVO.Definition.Repositories;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IVO.CMS
{
    public sealed class ContentEngine
    {
        private const string rootElementName = "_root_";

        private static readonly byte[] rootOpen = Encoding.UTF8.GetBytes("<" + rootElementName + ">");
        private static readonly byte[] rootClose = Encoding.UTF8.GetBytes("</" + rootElementName +">");

        private ITreeRepository trrepo;
        private IBlobRepository blrepo;
        private bool throwOnError;
        private List<SemanticError> errors;
        private bool injectErrorComments;
        private DateTimeOffset viewDate;

        public ContentEngine(ITreeRepository trrepo, IBlobRepository blrepo, DateTimeOffset viewDate, bool throwOnError = false, bool injectErrorComments = true)
        {
            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.viewDate = viewDate;
            this.throwOnError = throwOnError;
            this.injectErrorComments = injectErrorComments;
            this.errors = new List<SemanticError>();
        }

        public ReadOnlyCollection<SemanticError> GetErrors()
        {
            return new ReadOnlyCollection<SemanticError>(errors);
        }

        private void semanticError(XmlTextReader xr, StringBuilder sb, ContentItem item, string message)
        {
            var err = new SemanticError(message, item, xr.LineNumber, xr.LinePosition);

            if (throwOnError) throw err;

            // Track the error:
            errors.Add(err);

            // Inject an HTML comment describing the error:
            if (injectErrorComments)
                sb.AppendFormat("<!-- IVOCMS Error in '{0}' ({1}:{2}): {3} -->", err.Item.Path, err.LineNumber, err.LinePosition, err.Message);
        }

        /// <summary>
        /// Streaming copy from XmlTextReader and writing out to StringBuilder with event hooks for custom processing.
        /// </summary>
        /// <param name="xr"></param>
        /// <param name="sb"></param>
        /// <param name="action"></param>
        /// <param name="exit"></param>
        private void streamContent(XmlTextReader xr, StringBuilder sb, Func<XmlTextReader, bool> action, Func<XmlTextReader, bool> exit)
        {
            do
            {
                if (exit(xr)) break;
                if (!action(xr)) continue;

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

        private bool processCustomElementsAction(XmlTextReader xr, StringBuilder sb, ContentItem item)
        {
            if (xr.NodeType == XmlNodeType.Element && xr.LocalName.StartsWith("cms-"))
            {
                processCMSInstruction(xr.LocalName, xr, sb, item);
                
                // Skip normal copying behavior for this element:
                return false;
            }

            return true;
        }

        public HTMLFragment RenderContentItem(ContentItem item)
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
            StringBuilder sb = new StringBuilder(item.Blob.Contents.Length);

            // Start an XmlReader over the contents:
            using (MemoryStream ms = new MemoryStream(item.Blob.Contents))
            using (XmlTextReader xr = new XmlTextReader(ms, XmlNodeType.Element, new XmlParserContext(null, null, null, XmlSpace.Default)))
            {
                // Start reading the document:
                xr.Read();

                // Stream the content from the XmlTextReader, writing out to the StringBuilder and interpreting
                // custom elements along the way:
                streamContent(xr, sb, action: ixr => processCustomElementsAction(ixr, sb, item), exit: ixr => false);
            }

            string result = sb.ToString();
            return new HTMLFragment(result);
        }

        private void skipElementAndChildren(string elementName, XmlTextReader xr, StringBuilder sb, ContentItem item)
        {
            if (xr.NodeType != XmlNodeType.Element) semanticError(xr, sb, item, String.Format("expected start <{0}> element", elementName));
            if (xr.LocalName != elementName) semanticError(xr, sb, item, String.Format("expected start <{0}> element", elementName));
            if (xr.IsEmptyElement)
                return;
            
            int knownDepth = xr.Depth;

            // Read until we get back to the current depth:
            while (xr.Read() && xr.Depth > knownDepth) { }

            if (xr.NodeType != XmlNodeType.EndElement) semanticError(xr, sb, item, String.Format("expected end </{0}> element", elementName));
            if (xr.LocalName != elementName) semanticError(xr, sb, item, String.Format("expected end <{0}/> element", elementName));

            //xr.ReadEndElement(/* elementName */);
        }

        private void streamElementChildren(string elementName, XmlTextReader xr, StringBuilder sb, ContentItem item)
        {
            if (xr.NodeType != XmlNodeType.Element) semanticError(xr, sb, item, String.Format("expected start <{0}> element", elementName));
            if (xr.LocalName != elementName) semanticError(xr, sb, item, String.Format("expected start <{0}> element", elementName));
            // Nothing to do:
            if (xr.IsEmptyElement)
                return;

            int knownDepth = xr.Depth;
            // Shouldn't return false:
            if (!xr.Read()) semanticError(xr, sb, item, "could not read content after <content> start element");

            // Stream-copy and process inner custom cms- elements until we get back to the current depth:
            streamContent(xr, sb, action: xtr => processCustomElementsAction(xtr, sb, item), exit: xtr => (xtr.Depth == knownDepth));

            if (xr.NodeType != XmlNodeType.EndElement) semanticError(xr, sb, item, String.Format("expected end </{0}> element", elementName));
            if (xr.LocalName != elementName) semanticError(xr, sb, item, String.Format("expected end <{0}/> element", elementName));

            //xr.ReadEndElement(/* elementName */);
        }

        private void processCMSInstruction(string elementName, XmlTextReader xr, StringBuilder sb, ContentItem item)
        {
            int knownDepth = xr.Depth;

            // Skip the 'cms-' prefix and delegate to the instruction handlers:
            switch (elementName.Substring(4))
            {
                case "import": processImportElement(xr, sb, item); break;
                case "targeted": processTargetedElement(xr, sb, item); break;
                case "scheduled": processScheduledElement(xr, sb, item); break;
                case "list": throw new NotImplementedException();
                default:
                    // Not a built-in 'cms-' element name, check the custom element provider:
                    // TODO: toss this off to a provider model.

                    // Unrecognized 'cms-' element name, skip it entirely:
                    skipElementAndChildren(elementName, xr, sb, item);
                    break;
            }
        }

        private void processImportElement(XmlTextReader xr, StringBuilder sb, ContentItem item)
        {
            // Imports content directly from another blob, addressable by a relative path or an absolute path.
            // Relative path is always relative to the current blob's absolute path.
            // In the case of nested imports, relative paths are relative to the absolute path of the importee's parent blob.

            // <cms-import relative-path="../templates/main" />
            // <cms-import absolute-path="/templates/main" />

            // Absolute paths are canonicalized. An exception will be thrown if the path contains too many '..' references that
            // bring the canonicalized path above the root of the tree (which is impossible).

            // Recursively call RenderBlob on the imported blob and include the rendered HTMLFragment into this rendering.

            //skipElementAndChildren("cms-import", xr);

            // xr is pointing to "cms-import" Element.
            if (!xr.IsEmptyElement) semanticError(xr, sb, item, "cms-import element must be empty");
            
            if (xr.HasAttributes && xr.MoveToFirstAttribute())
            {
                // TODO
            }
        }

        private void processTargetedElement(XmlTextReader xr, StringBuilder sb, ContentItem item)
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
            skipElementAndChildren("cms-targeted", xr, sb, item);
        }

        private void processScheduledElement(XmlTextReader xr, StringBuilder sb, ContentItem item)
        {
            // Specifies that content should be scheduled for the entire month of August
            // and the entire month of October but NOT the month of September.
            // 'from' is inclusive date/time.
            // 'to'   is exclusive date/time.
            // <cms-scheduled>
            //   <range from="2011-08-01 00:00 -0500" to="2011-09-01 00:00 -0500" />
            //   <range from="2011-10-01 00:00 -0500" to="2011-11-01 00:00 -0500" />
            //   <content>
            //     ...
            //   </content>
            // </cms-scheduled>
            
            //skipElementAndChildren("cms-scheduled", xr);

            bool displayContent = false;

            while (xr.Read())
            {
                if (xr.NodeType != XmlNodeType.Element) continue;

                if (xr.LocalName == "range")
                {
                    // If we're already good to display, don't bother evaluating further schedule ranges:
                    if (displayContent) continue;

                    string fromAttr, toAttr;

                    // Validate the element's form:
                    if (!xr.IsEmptyElement) semanticError(xr, sb, item, "range element must be empty");
                    if (!xr.HasAttributes) semanticError(xr, sb, item, "range element must have attributes");
                    if ((fromAttr = xr.GetAttribute("from")) == null) semanticError(xr, sb, item, "range element must have 'from' attribute");
                    if ((toAttr = xr.GetAttribute("to")) == null) semanticError(xr, sb, item, "range element must have 'to' attribute");

                    // Parse the dates:
                    DateTimeOffset fromDate, toDateTmp;
                    DateTimeOffset toDate = DateTimeOffset.Now;

                    if (!DateTimeOffset.TryParse(fromAttr, out fromDate)) semanticError(xr, sb, item, "could not parse 'from' attribute as a date/time");
                    if (!String.IsNullOrWhiteSpace(toAttr))
                    {
                        if (!DateTimeOffset.TryParse(toAttr, out toDateTmp)) semanticError(xr, sb, item, "could not parse 'to' attribute as a date/time");
                        toDate = toDateTmp;
                    }

                    // Validate the range is ordered correctly:
                    if (toDate <= fromDate) semanticError(xr, sb, item, "'to' date must be later than 'from' date or empty");

                    // Check the schedule range:
                    if (viewDate >= fromDate && viewDate < toDate)
                        displayContent = true;
                }
                else if (xr.LocalName == "content")
                {
                    if (displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        streamElementChildren("content", xr, sb, item);
                        xr.ReadEndElement(/* "content" */);
                    }
                    else
                    {
                        skipElementAndChildren("content", xr, sb, item);
                    }
                    break;
                }
                else
                {
                    semanticError(xr, sb, item, "unexpected element");
                }
            }

            // TODO: check that the 'content' element was at least found, else semantic error.

            // Skip Whitespace and Comments etc. until we find the end element:
            while (xr.NodeType != XmlNodeType.EndElement && xr.Read()) { }

            // Validate:
            if (xr.LocalName != "cms-scheduled") semanticError(xr, sb, item, "expected end <cms-scheduled/> element");

            // Don't read this element because the next `xr.Read()` in the main loop will:
            //xr.ReadEndElement(/* "cms-scheduled" */);
        }
    }
}
