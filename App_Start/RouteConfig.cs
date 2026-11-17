using System.Web.Mvc;
using System.Web.Routing;
namespace LTW_25DH110785
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            var route=routes.MapRoute("Default","{controller}/{action}/{id}",new { controller="Home",action="Index",id=UrlParameter.Optional },new[] { "LTW_25DH110785.Controllers" });
            route.DataTokens["UseNamespaceFallback"]=false;
        }
    }
}
