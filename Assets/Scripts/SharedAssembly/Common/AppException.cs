// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

[System.Serializable]
public class AppException : System.Exception
{
    public AppException() { }

    public AppException(string message)
        : base(message) { }

    public AppException(string message, System.Exception inner)
        : base(message, inner) { }

    protected AppException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context
    )
        : base(info, context) { }
}
