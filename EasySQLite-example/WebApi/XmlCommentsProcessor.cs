namespace WebApi
{
    using System.Collections.Concurrent;
    using System.Reflection;
    using System.Xml.Linq;

    public static class XmlCommentsHelper
    {
        private static readonly ConcurrentDictionary<string, XElement> _memberCache = new();

        public static void LoadXmlComments(string xmlPath)
        {
            var xmlDoc = XDocument.Load(xmlPath);
            foreach (var member in xmlDoc.Descendants("member"))
            {
                var name = member.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    _memberCache[name] = member;
                }
            }
        }

        public static string GetMethodSummary(MethodInfo method)
        {
            var key = $"M:{method.DeclaringType?.FullName?.Replace("+", ".")}.{method.Name}";
            return GetSummary(key);
        }

        public static string GetParameterComment(MethodInfo method, string parameterName)
        {
            var key = $"M:{method.DeclaringType?.FullName?.Replace("+", ".")}.{method.Name}";
            return _memberCache.TryGetValue(key, out var member)
                ? member.Elements("param")
                        .FirstOrDefault(p => p.Attribute("name")?.Value == parameterName)?
                        .Value.Trim()
                : null;
        }

        public static string GetReturnsComment(MethodInfo method)
        {
            var key = $"M:{method.DeclaringType?.FullName?.Replace("+", ".")}.{method.Name}";
            return _memberCache.TryGetValue(key, out var member)
                ? member.Element("returns")?.Value.Trim()
                : null;
        }

        public static string GetPropertySummary(Type type, string propertyName)
        {
            var key = $"P:{type.FullName}.{propertyName}";
            return GetSummary(key);
        }

        private static string GetSummary(string key)
        {
            return _memberCache.TryGetValue(key, out var member)
                ? member.Element("summary")?.Value.Trim()
                : null;
        }
    }

}
