using System.Web.Mvc;
namespace LTW_25DH110785
{
    public static class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters) { filters.Add(new HandleErrorAttribute()); }
    }
}
