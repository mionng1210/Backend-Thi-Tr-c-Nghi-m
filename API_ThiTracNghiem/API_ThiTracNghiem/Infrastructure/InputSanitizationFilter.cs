using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Reflection;

namespace API_ThiTracNghiem.Infrastructure
{
    public class InputSanitizationFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            foreach (var kvp in context.ActionArguments.ToList())
            {
                var obj = kvp.Value;
                if (obj == null) continue;
                var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite);
                foreach (var p in props)
                {
                    var val = p.GetValue(obj) as string;
                    if (val == null) continue;
                    var cleaned = Sanitize(val);
                    p.SetValue(obj, cleaned);
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }

        private static string Sanitize(string s)
        {
            var trimmed = s.Trim();
            trimmed = trimmed.Replace("\0", string.Empty);
            return trimmed;
        }
    }
}

