using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;

namespace IVO.CMS.Providers.CustomElements
{
    public sealed class ConditionalElementProvider : ICustomElementProvider
    {
        private IConditionalEvaluator evaluator;

        public ConditionalElementProvider(IConditionalEvaluator evaluator, ICustomElementProvider next = null)
        {
            this.Next = next;
            this.evaluator = evaluator;
        }

        #region ICustomElementProvider Members

        public ICustomElementProvider Next { get; private set; }

        public async Task<bool> ProcessCustomElement(string elementName, RenderState state)
        {
            if (elementName != "cms-conditional") return false;

            await processConditionalElement(state).ConfigureAwait(continueOnCapturedContext: false);

            return true;
        }

        #endregion

        private enum ConditionalState
        {
            ExpectingIf,
            ExpectingElseOrElIf,
            ExpectingEnd
        }

        private async Task processConditionalElement(RenderState st)
        {
            // <cms-conditional>
            //     <if department="Sales">Hello, Sales dept!</if>
            //     <elif department="Accounting">Hello, Accounting dept!</elif>
            //     <elif department="Management">Hello, Management dept!</elif>
            //     <else>Hello, unknown dept!</else>
            // </cms-conditional>

            //st.SkipElementAndChildren("cms-conditional");

            ConditionalState c = ConditionalState.ExpectingIf;
            bool satisfied = false;
            bool condition = false;
            Dictionary<string, string> conditionVariables;

            int knownDepth = st.Reader.Depth;
            while (st.Reader.Read() && st.Reader.Depth > knownDepth)
            {
                // FIXME: should be non-whitespace check
                if (st.Reader.NodeType != XmlNodeType.Element) continue;

                switch (c)
                {
                    case ConditionalState.ExpectingIf:
                        if (st.Reader.LocalName != "if")
                        {
                            st.Error("expected 'if' element");
                            goto errored;
                        }

                        // Update state to expect 'elif' or 'else' elements:
                        c = ConditionalState.ExpectingElseOrElIf;
                        goto processCondition;
                    
                    case ConditionalState.ExpectingElseOrElIf:
                        if (st.Reader.LocalName == "elif")
                        {
                            c = ConditionalState.ExpectingElseOrElIf;
                            goto processCondition;
                        }
                        else if (st.Reader.LocalName == "else")
                        {
                            c = ConditionalState.ExpectingEnd;
                            goto processElse;
                        }
                        else
                        {
                            st.Error("expected 'elif' or 'else' element");
                            goto errored;
                        }

                    case ConditionalState.ExpectingEnd:
                        st.Error("expected </cms-conditional> end element");
                        break;

                    processCondition:
                        // Parse out the condition test variables:
                        conditionVariables = new Dictionary<string, string>(StringComparer.Ordinal);
                        if (st.Reader.HasAttributes && st.Reader.MoveToFirstAttribute())
                        {
                            do
                            {
                                conditionVariables.Add(st.Reader.LocalName, st.Reader.Value);
                            } while (st.Reader.MoveToNextAttribute());

                            st.Reader.MoveToElement();
                        }

                        // Make sure we have at least one:
                        if (conditionVariables.Count == 0)
                        {
                            st.Error("expected at least one attribute for '{0}' element", st.Reader.LocalName);
                            goto errored;
                        }

                        // Make sure the branch has not already been satisfied:
                        if (satisfied)
                        {
                            // Branch has already been satisfied, skip inner contents:
                            st.SkipElementAndChildren(st.Reader.LocalName);
                            break;
                        }

                        // Run the condition test variables through the evaluator chain:
                        IConditionalEvaluator eval = evaluator;
                        EitherAndOr? lastAndOr = null;
                        
                        while (eval != null)
                        {
                            bool test = eval.EvaluateConditional(conditionVariables);

                            if (lastAndOr.HasValue)
                            {
                                if (lastAndOr.Value == EitherAndOr.And) condition = condition && test;
                                else condition = condition || test;
                            }
                            else
                            {
                                condition = test;
                            }

                            lastAndOr = eval.AndOr;
                            eval = eval.Next;
                        }

                        // Now either render the inner content or skip it based on the `condition` evaluated:
                        if (condition)
                        {
                            satisfied = true;
                            // Copy inner contents:
                            await st.CopyElementChildren(st.Reader.LocalName).ConfigureAwait(continueOnCapturedContext: false);
                        }
                        else
                        {
                            // Skip inner contents:
                            st.SkipElementAndChildren(st.Reader.LocalName);
                        }
                        break;

                    processElse:
                        if (st.Reader.HasAttributes)
                        {
                            st.Error("unexpected attributes on 'else' element");
                            goto errored;
                        }

                        if (satisfied)
                        {
                            // Skip inner contents:
                            st.SkipElementAndChildren(st.Reader.LocalName);
                            break;
                        }

                        // Copy inner contents:
                        await st.CopyElementChildren(st.Reader.LocalName).ConfigureAwait(continueOnCapturedContext: false);
                        break;
                }
            }
            return;

        errored:
            // Keep reading to the end cms-conditional element:
            while (st.Reader.Read() && st.Reader.Depth > knownDepth) { }
            return;
        }
    }
}
