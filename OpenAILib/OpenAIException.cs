﻿// Copyright (c) 2023 Owen Sigurdson
// MIT License

namespace OpenAILib
{
    public class OpenAIException : Exception
    {
        public OpenAIException(string message) : base(message)
        {
        }
    }
}