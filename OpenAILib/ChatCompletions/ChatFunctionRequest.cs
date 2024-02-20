using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace OpenAILib.ChatCompletions
{
    class ChatFunctionRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("description")]
        public string Description { get; }

        [JsonPropertyName("parameters")]
        public ChatParametersRequest Parameters { get; }

        [JsonConstructor]
        public ChatFunctionRequest(string name, string description, ChatParametersRequest parameters)
        {
            Name = name;
            Description = description;
            Parameters = parameters;
        }
    }

    class ChatParametersRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; }

        [JsonPropertyName("properties")]
        public string Properties { get; }

        [JsonPropertyName("required")]
        public string[] Required { get; }

        [JsonConstructor]
        public ChatParametersRequest(string type, string properties, string[] required)
        {
            Type = type;
            Properties = properties;
            Required = required;
        }
    }
}
