using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using IVO.CMS.Providers;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using IVO.Definition.Repositories;

namespace IVO.CMS
{
    /// <summary>
    /// The main content blob renderer that outputs `HTMLFragment`s.
    /// </summary>
    public sealed class ContentEngine
    {
        private ITreeRepository trrepo;
        private IBlobRepository blrepo;
        private DateTimeOffset viewDate;
        private bool throwOnError;
        private bool injectErrorComments;
        private ICustomElementProvider providerRoot;

        private List<SemanticError> errors;

        /// <summary>
        /// Create a new content rendering engine with the provided customizations.
        /// </summary>
        /// <param name="trrepo"></param>
        /// <param name="blrepo"></param>
        /// <param name="viewDate"></param>
        /// <param name="evaluator"></param>
        /// <param name="provider"></param>
        /// <param name="throwOnError"></param>
        /// <param name="injectErrorComments"></param>
        public ContentEngine(ITreeRepository trrepo, IBlobRepository blrepo, DateTimeOffset viewDate, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null, bool throwOnError = false, bool injectErrorComments = true)
        {
            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.viewDate = viewDate;
            this.throwOnError = throwOnError;
            this.injectErrorComments = injectErrorComments;
            this.errors = new List<SemanticError>();

            // If no evaluator given, use the default false-returning evaluator:
            if (evaluator == null) evaluator = new DefaultFalseConditionalEvaluator(EitherAndOr.Or);

            // Wrap the given provider in the default chain:
            this.providerRoot =
                new ImportElementProvider(
                    new ScheduledElementProvider(
                        new ListElementProvider(
                            new LinkElementProvider(
                                new ConditionalElementProvider(evaluator, provider)
                            )
                        )
                    )
                );
        }

        /// <summary>
        /// Gets the passed-in `ITreeRepository` used to fetch trees.
        /// </summary>
        public ITreeRepository Trees { get { return trrepo; } }
        /// <summary>
        /// Gets the passed-in `IBlobRepository` used to fetch blobs.
        /// </summary>
        public IBlobRepository Blobs { get { return blrepo; } }
        /// <summary>
        /// Gets the date/time value used for scheduled content.
        /// </summary>
        public DateTimeOffset ViewDate { get { return viewDate; } }
        /// <summary>
        /// Gets the root element of the chain of custom-element providers.
        /// </summary>
        public ICustomElementProvider CustomElementProviderRoot { get { return providerRoot; } }

        /// <summary>
        /// Gets a value that indicates whether or not the rendering engine throws exceptions when errors are found while parsing.
        /// </summary>
        public bool ThrowOnError { get { return throwOnError; } }
        /// <summary>
        /// Gets a value that indicates whether or not the rendering engine injects HTML &lt;!-- comments --&gt; with error messages.
        /// </summary>
        public bool InjectErrorComments { get { return injectErrorComments; } }

        /// <summary>
        /// Gets the current collection of errors found while parsing the last content blob.
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<SemanticError> GetErrors()
        {
            return new ReadOnlyCollection<SemanticError>(errors);
        }

        /// <summary>
        /// Used by custom element providers to report an error at the current parsing location.
        /// </summary>
        /// <param name="err"></param>
        public void ReportError(SemanticError err)
        {
            if (throwOnError) throw err;

            // Track the error:
            errors.Add(err);
        }

        /// <summary>
        /// Main method to render the given blob as an HTML5 polyglot document fragment.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public HTMLFragment RenderBlob(BlobTreePath item)
        {
            // Refresh the error list:
            errors = new List<SemanticError>();

            RenderState rs = new RenderState(this);
            rs.Render(item);

            string result = rs.Writer.ToString();
            return new HTMLFragment(result);
        }
    }
}
