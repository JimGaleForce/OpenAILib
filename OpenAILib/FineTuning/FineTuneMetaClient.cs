﻿// Copyright (c) 2023 Owen Sigurdson
// MIT License

using OpenAILib.Files;
using OpenAILib.Serialization;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace OpenAILib.FineTuning
{
    internal class FineTuneMetaClient
    {
        private const string OpenAILibFileMarker = "openailib";
        private readonly FilesClient _filesClient;
        private readonly FineTunesClient _fineTunesClient;
        private static readonly ConcurrentDictionary<string, string> s_fineTuneIdToModelNameMap = new ConcurrentDictionary<string, string>();

        public FineTuneMetaClient(HttpClient httpClient)
        {
            _fineTunesClient = new FineTunesClient(httpClient);
            _filesClient = new FilesClient(httpClient);
        }

        public async Task<FineTuneInfo> CreateFineTuneAsync(List<FineTunePair> trainingData, CancellationToken cancellationToken = default)
        {
            return await CreateFineTuneAsync(trainingData, new FineTuneSpec01(), cancellationToken);
        }

        public async Task<FineTuneInfo> CreateFineTuneAsync(List<FineTunePair> trainingData, Action<IFineTuneSpec01> spec, CancellationToken cancellationToken = default)
        {
            var settings = new FineTuneSpec01();
            spec(settings);
            return await CreateFineTuneAsync(trainingData, settings, cancellationToken);
        }

        internal async Task<(bool, string?)> TryGetFineTuneModelNameAsync(FineTuneInfo fineTune, CancellationToken cancellationToken = default)
        {
            // Standard GetOrAdd method cannot be used with async responses. Code is a little clunkier
            // but the behavior is the same.
            if (s_fineTuneIdToModelNameMap.ContainsKey(fineTune.FineTuneId))
            {
                return (true, s_fineTuneIdToModelNameMap[fineTune.FineTuneId]);
            }

            var fineTuneResponse = await _fineTunesClient.GetFineTuneAsync(fineTune.FineTuneId, cancellationToken);
            var fineTunedModelName = fineTuneResponse.FineTunedModel;
            if (string.IsNullOrEmpty(fineTunedModelName))
            {
                return (false, default);
            }
            s_fineTuneIdToModelNameMap.TryAdd(fineTune.FineTuneId, fineTunedModelName);
            return (true, fineTunedModelName);
            
        }

        public async Task<List<FineTuneEvent>> GetEventsAsync(FineTuneInfo fineTune, CancellationToken cancellationToken = default)
        {
            var fineTuneResponse = await _fineTunesClient.GetFineTuneAsync(fineTune.FineTuneId, cancellationToken);
            var result = fineTuneResponse.Events
                .Select(evt => FineTuneEvent.FromFineTuneResponse(evt))
                .ToList();

            return result;
        }

        public async IAsyncEnumerable<FineTuneEvent> GetEventStreamAsync(FineTuneInfo fineTune, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var fineTuneEventResponse in _fineTunesClient.GetEventStreamAsync(fineTune.FineTuneId, cancellationToken))
            {
                if (string.IsNullOrEmpty(fineTuneEventResponse.Message))
                {
                    continue;
                }
                yield return FineTuneEvent.FromFineTuneResponse(fineTuneEventResponse);
            }
        }

        public async Task<FineTuneStatus> GetStatusAsync(FineTuneInfo fineTune, CancellationToken cancellationToken = default)
        {
            var fineTuneResponse = await _fineTunesClient.GetFineTuneAsync(fineTune.FineTuneId, cancellationToken);

            // TODO: verify that these strings are what is actually returned
            switch (fineTuneResponse.Status)
            {
                case "succeeded":
                    return FineTuneStatus.Succeeded;
                case "failed":
                    return FineTuneStatus.Failed;
                default:
                    break;
            }
            return FineTuneStatus.NotReady;
        }

        private async Task<FineTuneInfo> CreateFineTuneAsync(List<FineTunePair> trainingData, FineTuneSpec01 settings, CancellationToken cancellationToken = default)
        {
            var promptSuffix = settings.GetPromptSuffix();
            var completionSuffix = settings.GetCompletionSuffix();

            var lookup = await GetOpenAILibManagedFiles(cancellationToken);
            var processedTrainingData = FineTuneTrainingDataProcessor.ProcessFineTuneData(trainingData, promptSuffix, completionSuffix);
            var trainingFileId = await EnsureTrainingDataUploadedAsync(processedTrainingData, lookup, cancellationToken);

            string? validationFileId = null;
            var validationData = settings.GetValidationData();
            if (validationData.Count > 0)
            {
                var processedValidationData = FineTuneTrainingDataProcessor.ProcessFineTuneData(validationData, promptSuffix, completionSuffix);
                validationFileId = await EnsureTrainingDataUploadedAsync(processedValidationData, lookup, cancellationToken);
            }

            var fineTuneRequest = settings.ToRequest(trainingFileId, validationFileId);
            var fineTuneId = await _fineTunesClient.CreateFineTuneAsync(fineTuneRequest, cancellationToken);
            return new FineTuneInfo(fineTuneId, promptSuffix, completionSuffix);
        }

        private async Task<string> EnsureTrainingDataUploadedAsync(List<FineTunePair> trainingData, Dictionary<string, FileResponse> lookup, CancellationToken cancellationToken)
        {
            // provide allocation hint - assume at least 10 bytes per pair
            var memoryStream = new MemoryStream(trainingData.Count * 10);
            JsonLinesSerializer.Serialize(memoryStream, trainingData);
            memoryStream.Position = 0;
            var fileName = GetFileName(memoryStream.GetBuffer());
            string fileId;
            if (lookup.TryGetValue(fileName, out var fileInfo))
            {
                fileId = fileInfo.Id;
            }
            else
            {
                fileId = await _filesClient.UploadStreamAsync(memoryStream, FilePurpose.FineTune, fileName, cancellationToken);
            }
            return fileId;
        }

        private async Task<Dictionary<string, FileResponse>> GetOpenAILibManagedFiles(CancellationToken cancellationToken)
        {
            // creates a lookup for all OpenAILib (i.e. this library) managed files
            var allFiles = await _filesClient.GetFilesAsync(cancellationToken);
            var lookup = allFiles.Where(file => file.Filename.StartsWith(OpenAILibFileMarker))
                .ToDictionary(file => file.Filename, file => file);
            return lookup;
        }

        private static string GetFileName(ReadOnlySpan<byte> bytes)
        {
            var hashBytes = SHA1.HashData(bytes);
            return "openailib." + Convert.ToHexString(hashBytes).ToLower() + ".jsonl";
        }
    }
}
