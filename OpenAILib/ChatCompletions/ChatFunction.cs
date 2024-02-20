// Copyright (c) 2023 Owen Sigurdson
// MIT License

using Newtonsoft.Json;

namespace OpenAILib
{
    /// <summary>
    /// Represents an Open AI chat message completion with specified <paramref name="Role"/> and
    /// <paramref name="Message"/>
    /// </summary>
    /// <param name="Role">Role associated with the message</param>
    /// <param name="Message">Message text</param>
    public record ChatFunction(object aiFunction);

    [AttributeUsage(AttributeTargets.Class)]
    public class AIFunctionAttribute : Attribute
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public AIFunctionAttribute(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class AIPropertyAttribute : Attribute
    {
        public bool IsRequired { get; set; }

        public string Description { get; set; }

        public AIPropertyAttribute(bool isRequired = false, string description = null)
        {
            IsRequired = isRequired;
            Description = description;
        }

        public AIPropertyAttribute(string description)
        {
            Description = description;
        }
    }

    public static class GPTFunctionUtils
    {
        public static string GPTFunctionJson<T>(T aiFunction)
            where T : class
        {
            var functions = new List<Dictionary<string, object>>();

            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.GetCustomAttributes(typeof(AIFunctionAttribute), false).Any())
                .ToList();

            foreach (var type in types)
            {
                var functionAttr = type.GetCustomAttributes(typeof(AIFunctionAttribute), false).FirstOrDefault() as AIFunctionAttribute;
                var function = new Dictionary<string, object>
                {
                    { "name", functionAttr.Name },
                    { "description", functionAttr.Description },
                    { "parameters", GetParameters(type) }
                };
                functions.Add(function);
            }

            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            var json = JsonConvert.SerializeObject(functions, jsonSettings);
            return json;
        }

        private static Dictionary<string, object> GetParameters(Type type)
        {
            var properties = type.GetProperties();
            var parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", new Dictionary<string, Dictionary<string, string>>() },
                { "required", new List<string>() }
            };

            foreach (var property in properties)
            {
                var propertyAttr = property.GetCustomAttributes(typeof(AIPropertyAttribute), false).FirstOrDefault() as AIPropertyAttribute;
                var propertyDict = new Dictionary<string, string>
                { { "type", property.PropertyType.Name.ToLower() }, { "description", propertyAttr.Description } };

                if (property.PropertyType.IsEnum)
                {
                    propertyDict["type"] = "string";
                    propertyDict["enum"] = JsonConvert.SerializeObject(
                        Enum.GetNames(property.PropertyType).Select(e => e.ToLower()).ToArray());
                }

                ((Dictionary<string, Dictionary<string, string>>)parameters["properties"]).Add(
                    property.Name.ToLower(),
                    propertyDict);

                if (propertyAttr.IsRequired)
                {
                    ((List<string>)parameters["required"]).Add(property.Name.ToLower());
                }
            }

            return parameters;
        }
    }
}
