using System;
using System.Collections.Generic;
using Dreamy.Datasave;

namespace Dreamy.Progression.Samples
{
    // Read-only legacy DTO for importing old split inventory files into the shared wallet.
    [Serializable]
    public sealed class DemoInventorySave : SaveData
    {
        public long Count;
        public Dictionary<string, string> Receipts = new();
    }
}
