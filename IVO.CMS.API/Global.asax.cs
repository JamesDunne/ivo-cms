using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace IVO.CMS.API
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Render",
                "render/{*rootedPath}",
                new { controller = "Render", action = "render" },
                new { rootedPath = @"[\w\d]{40}/.*" }
            );

            routes.MapRoute(
                "BlobGetPath",
                "blob/get/tree/{*rootedPath}",
                new { controller = "Blob", action = "getByPath" },
                new { rootedPath = @"[\w\d]{40}/.*" }
            );

            routes.MapRoute(
                "BlobGetID",
                "blob/get/blob/{id}",
                new { controller = "Blob", action = "get" }
            );

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterRoutes(RouteTable.Routes);
        }
    }
}