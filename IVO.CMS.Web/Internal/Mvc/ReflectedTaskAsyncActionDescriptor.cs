using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Mvc.Async;

namespace IVO.CMS.Web.Internal.Mvc
{
    public class ReflectedTaskAsyncActionDescriptor : AsyncActionDescriptor
    {
        ReflectedActionDescriptor _actionDescriptor;

        protected ReflectedTaskAsyncActionDescriptor(ReflectedActionDescriptor actionDescriptor)
        {
            _actionDescriptor = actionDescriptor;
        }

        public override IAsyncResult BeginExecute(ControllerContext controllerContext, IDictionary<string, object> parameters, AsyncCallback callback, object state)
        {
            var task = _actionDescriptor.Execute(controllerContext, parameters) as Task;
            task.ContinueWith(t => callback(t));
            //
            // Its important that the original task is returned, 
            // because EndExecute is handed that task, and not that one passed in callback....???!
            //
            return task;
        }

        public override object EndExecute(IAsyncResult asyncResult)
        {
            return GetResult(asyncResult);
        }

        private object GetResult(IAsyncResult asyncResult)
        {
            var taskOfActionResult = asyncResult as Task<ActionResult>;

            if (taskOfActionResult != null)
            {
                return taskOfActionResult.Result;
            }

            //
            // Look for more standard result types?
            // Do some hack to get the result out?
            //
            throw new NotSupportedException();
        }

        public static ActionDescriptor CreateIfTaskAsync(ActionDescriptor actionDescriptor)
        {
            var reflectedActionDescriptor = actionDescriptor as ReflectedActionDescriptor;
            if (reflectedActionDescriptor != null &&
                (reflectedActionDescriptor.MethodInfo.ReturnType == typeof(Task<ActionResult>) || IsTaskOfActionResult(reflectedActionDescriptor.MethodInfo.ReturnType)))
            {
                return new ReflectedTaskAsyncActionDescriptor(reflectedActionDescriptor);
            }
            return actionDescriptor;
        }

        private static bool IsTaskOfActionResult(Type type)
        {
            var taskType = typeof(Task<>);
            var actionResultType = typeof(ActionResult);

            if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                if (genericTypeDef == taskType)
                {
                    return actionResultType.IsAssignableFrom(type.GetGenericArguments()[0]);
                }
            }

            return false;
        }

        #region Wrapped Members

        public override string ActionName
        {
            get { return _actionDescriptor.ActionName; }
        }

        public override ControllerDescriptor ControllerDescriptor
        {
            get { return _actionDescriptor.ControllerDescriptor; }
        }

        public override ParameterDescriptor[] GetParameters()
        {
            return _actionDescriptor.GetParameters();
        }

        #endregion
    }
}
