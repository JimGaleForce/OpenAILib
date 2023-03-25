﻿// Copyright (c) 2023 Owen Sigurdson
// MIT License

using OpenAILib.ResponseCaching;

namespace OpenAILib.Tests
{
    [TestClass]
    public class OpenAIClientTest
    {
        [TestMethod]
        public async Task TestGetCompletionWithoutCaching()
        {
            // arrange
            var args = new OpenAIClientArgs(
                organizationId: TestCredentials.OrganizationId,
                apiKey: TestCredentials.ApiKey);

            var client = new OpenAIClient(args);

            // act
            var completion = await client.GetCompletionAsync("1 + 1 = ");

            // assert
            StringAssert.Contains(completion, "2");
        }

        [TestMethod]
        public async Task TestGetEmbeddingWithoutCaching()
        {
            // arrange
            var args = new OpenAIClientArgs(
                organizationId: TestCredentials.OrganizationId,
                apiKey: TestCredentials.ApiKey);

            var client = new OpenAIClient(args);

            // act
            var vector = await client.GetEmbeddingAsync("dog");

            // assert
            Assert.AreEqual(1536, vector.Length);
        }

        [TestMethod]
        public async Task TestGetCompletionAsyncWithCaching()
        {
            // arrange
            var dictionary = new Dictionary<Guid, string>();
            var cache = new DictionaryResponseCache(dictionary);

            var client = new OpenAIClient(
                new OpenAIClientArgs(organizationId: TestCredentials.OrganizationId, apiKey: TestCredentials.ApiKey)
                {
                    ResponseCache = cache
                });

            // act
            var completion = await client.GetCompletionAsync("1+1=");

            // assert
            Assert.IsTrue(completion.Contains("2"));
            Assert.AreEqual(1, dictionary.Count);
        }

        [TestMethod]
        public async Task TestGetCompletionSuggestColorExample()
        {
            var client = new OpenAIClient(new OpenAIClientArgs(organizationId: TestCredentials.OrganizationId, apiKey: TestCredentials.ApiKey));
            const string prompt = @"
                The CSS code for a color like a blue sky at dusk:

                background-color: #";

            var responseText = await client.GetCompletionAsync(prompt, spec => spec
                                                                        .Model(CompletionModels.TextDavinci0003)
                                                                        .Temperature(0)
                                                                        .MaxTokens(64)
                                                                        .TopProbability(1)
                                                                        .FrequencyPenalty(0)
                                                                        .PresencePenalty(0)
                                                                        .Stop(";"));

            // I got 3c5a99
            Assert.IsTrue(responseText.Length > 3);
        }

        [TestMethod]
        public async Task TestGetCompletionAsyncEnglishToOtherLanguagesExample()
        {
            var client = new OpenAIClient(new OpenAIClientArgs(organizationId: TestCredentials.OrganizationId, apiKey: TestCredentials.ApiKey));
            const string prompt = @"
                         Translate this into 1. French, 2. Spanish and 3. Japanese:
                         What rooms do you have available?

                         1.";

            var responseText = await client.GetCompletionAsync(prompt, spec => spec
                                                                                .Model(CompletionModels.TextDavinci0003)
                                                                                .Temperature(0.3)
                                                                                .MaxTokens(100)
                                                                                .TopProbability(1)
                                                                                .FrequencyPenalty(0)
                                                                                .PresencePenalty(0));

            // Response I got...

            //  Quels sont les chambres que vous avez disponibles ?
            // 2. ¿Qué habitaciones tienes disponibles?
            // 3.どの部屋が利用可能ですか？
            StringAssert.Contains(responseText, "2. ");
        }

        [TestMethod]
        public async Task TestGetChatCompletionAsync()
        {
            var client = new OpenAIClient(new OpenAIClientArgs(organizationId: TestCredentials.OrganizationId, apiKey: TestCredentials.ApiKey));
            var sequence = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are a helpful assistant"),
                    new ChatMessage(ChatRole.User, "What is a good name for a cat?")
                };

            var response = await client.GetChatCompletionAsync(sequence);

            // Response I got...
            // There are many great names for cats
            Assert.IsTrue(response.Length > 5);

        }

        [TestMethod]
        public async Task TestGetChatCompletionAsyncWithSpec()
        {
            var client = new OpenAIClient(new OpenAIClientArgs(organizationId: TestCredentials.OrganizationId, apiKey: TestCredentials.ApiKey));
            var sequence = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System, "You are a helpful assistant"),
                    new ChatMessage(ChatRole.User, "What is a good name for a cat?")
                };

            var response = await client.GetChatCompletionAsync(sequence, spec => spec.MaxTokens(5).Temperature(0).User("fred"));

            // Response I got... (cut off due to only 5 tokens Max
            // There are many great

            StringAssert.Contains(response, "There");
        }

        private class DictionaryResponseCache : IResponseCache
        {
            private readonly Dictionary<Guid, string> _cache = new();

            public DictionaryResponseCache(Dictionary<Guid, string> cache)
            {
                _cache = cache;
            }

            public void PutResponse(Guid key, string response)
            {
                _cache[key] = response;
            }

            public bool TryGetResponseAsync(Guid key, out string? cachedResponse)
            {
                return _cache.TryGetValue(key, out cachedResponse);
            }

            public Dictionary<Guid, string> GetUnderlyingDictionary()
            {
                return _cache;
            }
        }
    }
}
