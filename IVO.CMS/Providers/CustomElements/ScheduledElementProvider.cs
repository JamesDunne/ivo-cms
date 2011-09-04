using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ScheduledElementProvider : ICustomElementProvider
    {
        public ScheduledElementProvider(ICustomElementProvider next = null)
        {
            this.Next = next;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public bool ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-scheduled") return false;

            processScheduledElement(state);

            return true;
        }

        #endregion

        private void processScheduledElement(RenderState st)
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

            int knownDepth = st.Reader.Depth;
            while (st.Reader.Read() && st.Reader.Depth > knownDepth)
            {
                if (st.Reader.NodeType != XmlNodeType.Element) continue;

                if (st.Reader.LocalName == "range")
                {
                    hasRanges = true;

                    if (!st.Reader.IsEmptyElement)
                    {
                        st.Error("range element must be empty");
                        // TODO: skip to end of cms-scheduled element and exit.
                        continue;
                    }

                    // If we're already good to display, don't bother evaluating further schedule ranges:
                    if (displayContent)
                        // Safe to continue here because the element is empty; no more to parse.
                        continue;

                    string fromAttr, toAttr;

                    // Validate the element's form:
                    if (!st.Reader.HasAttributes) st.Error("range element must have attributes");
                    if ((fromAttr = st.Reader.GetAttribute("from")) == null) st.Error("range element must have 'from' attribute");
                    // 'to' attribute is optional:
                    toAttr = st.Reader.GetAttribute("to");

                    // Parse the dates:
                    DateTimeOffset fromDate, toDateTmp;
                    DateTimeOffset toDate = DateTimeOffset.Now;

                    if (!DateTimeOffset.TryParse(fromAttr, out fromDate)) st.Error("could not parse 'from' attribute as a date/time");
                    if (!String.IsNullOrWhiteSpace(toAttr))
                    {
                        if (DateTimeOffset.TryParse(toAttr, out toDateTmp))
                            toDate = toDateTmp;
                        else
                            st.Error("could not parse 'to' attribute as a date/time");
                    }

                    // Validate the range's dates are ordered correctly:
                    if (toDate <= fromDate) st.Error("'to' date must be later than 'from' date or empty");

                    // Check the schedule range:
                    displayContent = (st.Engine.ViewDate >= fromDate && st.Engine.ViewDate < toDate);
                }
                else if (st.Reader.LocalName == "content")
                {
                    if (hasContent) st.Error("only one content element may exist in cms-scheduled");

                    hasContent = true;
                    if (!hasRanges) st.Error("no range elements found before content element in cms-scheduled");

                    if (displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        st.CopyElementChildren("content");
                        st.Reader.ReadEndElement(/* "content" */);
                    }
                    else
                    {
                        // Skip the inner content entirely:
                        st.SkipElementAndChildren("content");
                        st.Reader.ReadEndElement(/* "content" */);
                    }
                }
                else if (st.Reader.LocalName == "else")
                {
                    if (hasElse) st.Error("only one else element may exist in cms-scheduled");
                    hasElse = true;

                    if (!displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        st.CopyElementChildren("else");
                        st.Reader.ReadEndElement(/* "else" */);
                    }
                    else
                    {
                        // Skip the inner content entirely:
                        st.SkipElementAndChildren("else");
                        st.Reader.ReadEndElement(/* "else" */);
                    }
                }
                else
                {
                    st.Error(String.Format("unexpected element '{0}'", st.Reader.LocalName));
                }
            }

            // Report errors:
            if (!hasRanges) st.Error("no range elements found");
            if (!hasContent) st.Error("no content element found");

            // Skip Whitespace and Comments etc. until we find the end element:
            while (st.Reader.NodeType != XmlNodeType.EndElement && st.Reader.Read()) { }

            // Validate:
            if (st.Reader.LocalName != "cms-scheduled") st.Error("expected end <cms-scheduled/> element");

            // Don't read this element because the next `st.Reader.Read()` in the main loop will:
            //st.Reader.ReadEndElement(/* "cms-scheduled" */);
        }
    }
}
