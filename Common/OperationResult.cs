using System;
using System.Collections.Generic;

namespace DTS.Common;

internal sealed class OperationResult
{
    private readonly List<string> _messages = [];
    public bool IsSuccess => _messages.Count == 0;

    public void AddMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _messages.Add(message);
    }

    public override string ToString() =>
        string.Join(Environment.NewLine, _messages);
}