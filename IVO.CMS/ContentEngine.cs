using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using IVO.CMS.Providers;
using IVO.CMS.Providers.CustomElements;
using IVO.Definition.Models;
using IVO.Definition.Repositories;
using System.Threading.Tasks;

namespace IVO.CMS
{
    /// <summary>
    /// The main content blob renderer that outputs `HTMLFragment`s.
    /// </summary>
    public sealed class ContentEngine
    {
        private ITreeRepository trrepo;
        private IStreamedBlobRepository blrepo;
        private ITreePathStreamedBlobRepository tpsbrepo;

        private DateTimeOffset viewDate;
        private bool throwOnError;
        private bool injectErrorComments;
        private bool injectWarningComments;
        private ICustomElementProvider providerRoot;

        private List<SemanticError> errors;
        private List<SemanticWarning> warnings;

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
        /// <param name="injectWarningComments"></param>
        public ContentEngine(ITreeRepository trrepo, IStreamedBlobRepository blrepo, ITreePathStreamedBlobRepository tpsbrepo, DateTimeOffset viewDate, IConditionalEvaluator evaluator = null, ICustomElementProvider provider = null, bool throwOnError = false, bool injectErrorComments = true, bool injectWarningComments = true)
        {
            this.trrepo = trrepo;
            this.blrepo = blrepo;
            this.tpsbrepo = tpsbrepo;
            this.viewDate = viewDate;
            this.throwOnError = throwOnError;
            this.injectErrorComments = injectErrorComments;
            this.injectWarningComments = injectWarningComments;
            this.errors = new List<SemanticError>();
            this.warnings = new List<SemanticWarning>();

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
        public IStreamedBlobRepository StreamedBlobs { get { return blrepo; } }
        /// <summary>
        /// Gets the passed-in `ITreePathStreamedBlobRepository` used to fetch blobs by `TreePath`s.
        /// </summary>
        public ITreePathStreamedBlobRepository TreePathStreamedBlobs { get { return tpsbrepo; } }
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
        /// Gets a value that indicates whether or not the rendering engine injects HTML &lt;!-- comments --&gt; for error messages.
        /// </summary>
        public bool InjectErrorComments { get { return injectErrorComments; } }
        /// <summary>
        /// Gets a value that indicates whether or not the rendering engine injects HTML &lt;!-- comments --&gt; for warning messages.
        /// </summary>
        public bool InjectWarningComments { get { return injectWarningComments; } }

        /// <summary>
        /// Gets the current collection of errors found while parsing the last content blob.
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<SemanticError> GetErrors()
        {
            return new ReadOnlyCollection<SemanticError>(errors);
        }

        /// <summary>
        /// Gets the current collection of warnings found while parsing the last content blob.
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<SemanticWarning> GetWarnings()
        {
            return new ReadOnlyCollection<SemanticWarning>(warnings);
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
        /// Used by custom element providers to report a warning at the current parsing location.
        /// </summary>
        /// <param name="warn"></param>
        public void ReportWarning(SemanticWarning warn)
        {
            // Track the warning:
            warnings.Add(warn);
        }

        /// <summary>
        /// Main method to render the given blob as an HTML5 polyglot document fragment.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<HTMLFragment> RenderBlob(TreePathStreamedBlob item)
        {
            // Refresh the error and warning lists:
            errors = new List<SemanticError>( (int)((item.StreamedBlob.Length ?? 16384L) / 5L) );
            warnings = new List<SemanticWarning>();

            RenderState rs = new RenderState(this);
            var writer = await rs.Render(item);

            string result = writer.ToString();
            return new HTMLFragment(result);
        }
    }
}
