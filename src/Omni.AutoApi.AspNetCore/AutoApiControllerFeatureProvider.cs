using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Omni.AutoApi.AspNetCore
{
    public class AutoApiControllerFeatureProvider : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo)
        {
            if (!typeInfo.IsClass || typeInfo.IsAbstract)
            {
                return false;
            }

            if (AutoApiHelper.IsAutoApiController(typeInfo))
            {
                return true;
            }

            return base.IsController(typeInfo);
        }
    }
}
