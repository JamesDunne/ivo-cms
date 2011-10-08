using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace IVO.CMS.Web.Mvc
{
    public class TaskAsyncController : AsyncController
    {
        protected override IActionInvoker CreateActionInvoker()
        {
            return new TaskAsyncControllerActionInvoker();
        }
    }
}
