using System;
using System.Xml;
using System.Threading.Tasks;

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

        public async Task<bool> ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-scheduled") return false;

            await processScheduledElement(state).ConfigureAwait(continueOnCapturedContext: false);

            return true;
        }

        #endregion

        private async Task processScheduledElement(RenderState st)
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

            XmlTextReader xr = st.Reader;

            int knownDepth = xr.Depth;
            while (xr.Read() && xr.Depth > knownDepth)
            {
                if (xr.NodeType != XmlNodeType.Element) continue;

                if (xr.LocalName == "range")
                {
                    hasRanges = true;

                    if (!xr.IsEmptyElement)
                    {
                        st.Error("'range' element must be empty");
                        // Skip to end of cms-scheduled element and exit.
                        st.SkipElementAndChildren("range");
                        continue;
                    }

                    // If we're already good to display, don't bother evaluating further schedule ranges:
                    if (displayContent)
                        // Safe to continue here because the element is empty; no more to parse.
                        continue;

                    string fromAttr, toAttr;

                    // Validate the element's form:
                    if (!xr.HasAttributes) st.Error("range element must have attributes");
                    if ((fromAttr = xr.GetAttribute("from")) == null) st.Error("'range' element must have 'from' attribute");
                    // 'to' attribute is optional:
                    toAttr = xr.GetAttribute("to");

                    // Parse the dates:
                    DateTimeOffset fromDate, toDateTmp;
                    DateTimeOffset toDate;

                    if (!DateTimeOffset.TryParse(fromAttr, out fromDate))
                    {
                        st.Error("could not parse 'from' attribute as a date/time");
                        continue;
                    }
                    if (!String.IsNullOrWhiteSpace(toAttr))
                    {
                        if (DateTimeOffset.TryParse(toAttr, out toDateTmp))
                            toDate = toDateTmp;
                        else
                        {
                            st.Error("could not parse 'to' attribute as a date/time");
                            continue;
                        }
                    }
                    else
                    {
                        toDate = st.Engine.ViewDate;
                    }

                    // Validate the range's dates are ordered correctly:
                    if (toDate <= fromDate) st.Error("'to' date must be later than 'from' date or empty");

                    // Check the schedule range:
                    displayContent = (st.Engine.ViewDate >= fromDate && st.Engine.ViewDate < toDate);
                }
                else if (xr.LocalName == "content")
                {
                    if (hasElse)
                    {
                        st.Error("'content' element must come before 'else' element");
                        st.SkipElementAndChildren("content");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "content" */);
                        continue;
                    }

                    if (hasContent)
                    {
                        st.Error("only one 'content' element may exist in cms-scheduled");
                        st.SkipElementAndChildren("content");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "content" */);
                        continue;
                    }

                    hasContent = true;
                    if (!hasRanges)
                    {
                        st.Error("no 'range' elements found before 'content' element in cms-scheduled");
                        displayContent = false;
                    }

                    if (displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        await st.CopyElementChildren("content").ConfigureAwait(continueOnCapturedContext: false);
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "content" */);
                    }
                    else
                    {
                        // Skip the inner content entirely:
                        st.SkipElementAndChildren("content");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "content" */);
                    }
                }
                else if (xr.LocalName == "else")
                {
                    if (!hasContent)
                    {
                        st.Error("'content' element must come before 'else' element");
                        st.SkipElementAndChildren("else");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "else" */);
                        continue;
                    }
                    if (hasElse)
                    {
                        st.Error("only one 'else' element may exist in cms-scheduled");
                        st.SkipElementAndChildren("else");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "else" */);
                        continue;
                    }

                    hasElse = true;
                    if (!hasRanges)
                    {
                        st.Error("no 'range' elements found before 'else' element in cms-scheduled");
                        st.SkipElementAndChildren("else");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "else" */);
                        continue;
                    }

                    if (!displayContent)
                    {
                        // Stream the inner content into the StringBuilder until we get back to the end </content> element.
                        await st.CopyElementChildren("else").ConfigureAwait(continueOnCapturedContext: false);
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "else" */);
                    }
                    else
                    {
                        // Skip the inner content entirely:
                        st.SkipElementAndChildren("else");
                        if (!xr.IsEmptyElement)
                            xr.ReadEndElement(/* "else" */);
                    }
                }
                else
                {
                    st.Error("unexpected element '{0}'", xr.LocalName);
                }
            }

            // Report errors:
            if (!hasContent && !hasRanges) st.Error("no 'range' elements found");
            if (!hasContent) st.Error("no 'content' element found");

            // Skip Whitespace and Comments etc. until we find the end element:
            while (xr.NodeType != XmlNodeType.EndElement && xr.Read()) { }

            // Validate:
            if (xr.LocalName != "cms-scheduled") st.Error("expected end <cms-scheduled/> element");

            // Don't read this element because the next `xr.Read()` in the main loop will:
            //xr.ReadEndElement(/* "cms-scheduled" */);
        }
    }
}
